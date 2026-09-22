using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Infrastructure.Pdf.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors;
using Services.Command.Company;
using Services.Command.Invoice;
using Services.Command.List;
using Services.Command.Product;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;
using Services.Response.Invoice;
using Services.Response.Pdf;


namespace Services.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IEntityAuthorizationService _entityAuth;
        private readonly IInvoicePdfGenerator _pdf;
        private readonly ICancellationTokenAccessor _ctAccessor;

        private CancellationToken _ct => _ctAccessor.Token;

        public InvoiceService(
            AppDbContext context,
            ILogger<InvoiceService> logger,
            IEntityAuthorizationService entityAuth,
            IInvoicePdfGenerator pdf,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
            _pdf = pdf;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid companyId)
        {
            var companyExists = await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == companyId, _ct);

            if (!companyExists)
            {
                _logger.LogInformation("Company with id {CompanyId} not found.", companyId);
                return Result<List<CompanyDebtSummaryResponse>>.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            var unpaidInvoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId && i.PaidAmount < i.TotalAmount)
                .Select(i => new
                {
                    i.Id,
                    i.TotalAmount,
                    i.PaidAmount,
                    CurrencyCode = i.Currency.Code,
                    DecimalPlaces = i.Currency.DecimalPlaces,
                    i.CurrencyId
                })
                .ToListAsync(_ct);

            var corruptedInvoice = unpaidInvoices.FirstOrDefault(i =>
                i.CurrencyId == Guid.Empty ||
                string.IsNullOrWhiteSpace(i.CurrencyCode) ||
                i.DecimalPlaces < 0 ||
                i.PaidAmount < 0 ||
                i.TotalAmount < 0);

            if (corruptedInvoice != null)
            {
                _logger.LogError("Critical data corruption: Invoice {InvoiceId} has corrupted amounts or invalid currency relation.", corruptedInvoice.Id);
                throw new DataCorruptionException($"Financial data integrity violation for invoice '{corruptedInvoice.Id}'.");
            }

            var summary = unpaidInvoices
                .GroupBy(i => new
                {
                    i.CurrencyCode,
                    i.DecimalPlaces
                })
                .Select(g => new CompanyDebtSummaryResponse
                {
                    CurrencyCode = g.Key.CurrencyCode,
                    DecimalPlace = g.Key.DecimalPlaces,
                    TotalAmount = g.Sum(i => i.TotalAmount - i.PaidAmount)
                })
                .ToList();

            return Result<List<CompanyDebtSummaryResponse>>.Success(
                message: "Debt summary retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: summary
            );
        }

        public async Task<Result<PagedResult<CompanyDebtDetailResponse>>> GetCompanyDebtsAsync(CompanyCommand command)
        {
            var companyExists = await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == command.CompanyId, _ct);

            if (!companyExists)
            {
                _logger.LogInformation("Company with id {CompanyId} not found.", command.CompanyId);
                return Result<PagedResult<CompanyDebtDetailResponse>>.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            var hasCorruptedInvoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.CompanyId == command.CompanyId && i.PaidAmount < i.TotalAmount)
                .AnyAsync(i => i.CurrencyId == Guid.Empty || i.PaidAmount < 0 || i.TotalAmount < 0, _ct);

            if (hasCorruptedInvoices)
            {
                _logger.LogError("Critical data corruption: Found invoices with invalid currency or negative amounts for company {CompanyId}.", command.CompanyId);
                throw new DataCorruptionException($"Company '{command.CompanyId}' contains corrupted invoices.");
            }

            var now = DateTime.UtcNow;

            return await _context.Invoices
                .AsNoTracking()
                .Where(i => i.CompanyId == command.CompanyId && i.PaidAmount < i.TotalAmount)
                .OrderBy(i => i.DueDate)
                .Select(i => new CompanyDebtDetailResponse
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    AmountLeft = i.TotalAmount - i.PaidAmount,
                    CurrencyCode = i.Currency.Code,
                    DecimalPlaces = i.Currency.DecimalPlaces,
                    DueDate = i.DueDate,
                    DaysOverdue = i.DueDate < now ? (int)(now - i.DueDate).TotalDays : 0
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_debt", _ct);
        }

        public async Task<Result<PagedResult<InvoiceResponse>>> GetInvoiceListAsync(InvoiceListCommand command)
            => await _context.Invoices
                .AsNoTracking()
                .ApplySearch(command.SearchTerm)
                .ApplySorting(command.SortBy, command.SortDescending)
                .ApplyFilter(
                    command.CompanyName,
                    command.CompanyNip,
                    command.IssueDateFrom,
                    command.IssueDateTo,
                    command.TotalAmountFrom,
                    command.TotalAmountTo,
                    command.IsOverDue
                )
                .Select(i => new InvoiceResponse
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    IssueDate = i.IssueDate,
                    PaymentDate = i.PaymentDate,
                    CurrencyCode = i.Currency.Code,
                    DecimalPlaces = i.Currency.DecimalPlaces,
                    CompanyName = i.Company.Name,
                    CompanyNip = i.Company.NIP,
                    RemainingAmount = i.RemainingAmount,
                    IsOverDue = i.IsOverDue,
                    DueDate = i.DueDate,
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "invoice_list", _ct);

        public async Task<Result<InvoiceDetailResponse>> GetInvoiceDetailAsync(Guid invoiceId)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new InvoiceDetailResponse
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    IssueDate = i.IssueDate,
                    DueDate = i.DueDate,
                    PaymentDate = i.PaymentDate,
                    CompanyId = i.CompanyId,
                    CompanyName = i.Company.Name,
                    CompanyNip = i.Company.NIP,
                    DealId = i.DealId,
                    DealName = i.Deal != null ? i.Deal.Name : null
                })
                .FirstOrDefaultAsync(_ct);

            if (invoice == null)
            {
                _logger.LogInformation("Invoice with id {InvoiceId} not found.", invoiceId);
                return Result<InvoiceDetailResponse>.Failure(
                    message: "Invoice not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.InvoiceNotFound
                );
            }

            _logger.LogInformation("Invoice details for id {InvoiceId} retrieved successfully.", invoiceId);
            return Result<InvoiceDetailResponse>.Success(
                message: "Invoice details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: invoice
            );
        }

        public async Task<Result<PagedResult<InvoiceProductsListResponse>>> GetInvoiceProductAsync(Guid invoiceId, SimpleListCommand command)
             => await _context.InvoiceProducts
                      .AsNoTracking()
                      .Where(ip => ip.InvoiceId == invoiceId)
                      .ApplyProductSearch(command.SearchTerm)
                      .OrderBy(ip => ip.ProductName)
                      .Select(ip => new InvoiceProductsListResponse
                      {
                          InvoiceProductId = ip.Id,
                          ProductName = ip.ProductName,
                          SteelGrade = ip.SteelGrade,
                          UnitSymbol = ip.UnitSymbol,
                          Quantity = ip.Quantity,
                          UnitPrice = ip.UnitPrice,
                          TotalPrice = ip.TotalPrice,
                      })
                     .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "invoice_product_list", _ct);

        public async Task<Result<InvoicePaymentSummaryResponse>> GetInvoicePaymentSummaryAsync(Guid invoiceId)
        {
            var summary = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new InvoicePaymentSummaryResponse
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    RemainingAmount = i.TotalAmount - i.PaidAmount,
                    CurrencyCode = i.Currency.Code,
                    DecimalPlaces = i.Currency.DecimalPlaces,
                    DueDate = i.DueDate,
                    PaymentDate = i.PaymentDate,
                    IsOverDue = (i.TotalAmount - i.PaidAmount) > 0 && i.DueDate < DateTime.UtcNow,
                    PaymentsCount = i.Payments.Count
                })
                .FirstOrDefaultAsync(_ct);

            if (summary == null)
            {
                _logger.LogInformation("Invoice with id {InvoiceId} not found.", invoiceId);
                return Result<InvoicePaymentSummaryResponse>.Failure(
                    message: "Invoice not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.InvoiceNotFound
                );
            }

            return Result<InvoicePaymentSummaryResponse>.Success(
                message: "Invoice payment summary retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: summary
            );
        }

        public async Task<Result<PagedResult<InvoicePaymentListResponse>>> GetInvoicePaymentsAsync(Guid invoiceId, SimpleListCommand command)
            => await _context.InvoicePayments
                .AsNoTracking()
                .Where(p => p.InvoiceId == invoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ApplyPaymentSearch(command.SearchTerm)
                .Select(p => new InvoicePaymentListResponse
                {
                    PaymentId = p.Id,
                    Amount = p.Amount,
                    CurrencyCode = p.Invoice.Currency.Code,
                    DecimalPlaces = p.Invoice.Currency.DecimalPlaces,
                    PaymentDate = p.PaymentDate,
                    ReferenceNumber = p.ReferenceNumber,
                    Note = p.Note,
                    CreatedByFirstName = p.CreatedBy != null ? p.CreatedBy.FirstName : null,
                    CreatedByLastName = p.CreatedBy != null ? p.CreatedBy.LastName : null
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "invoice_payment_list", _ct);

        public async Task<Result> AddInvoicePaymentAsync(Guid invoiceId, Guid userId, AddInvoicePaymentCommand command)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Deal)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
            {
                _logger.LogInformation("Invoice with id {InvoiceId} not found.", invoiceId);
                return Result.Failure(
                    message: "Invoice not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.InvoiceNotFound
                );
            }

            var isAuthorized = await _entityAuth.CanModifyAsync(userId, invoice.Deal.OwnerId);
            if (!isAuthorized)
            {
                _logger.LogWarning("User {UserId} unauthorized to add payment for invoice {InvoiceId}.", userId, invoiceId);
                throw new ForbiddenException("You are not authorized to add payments to this invoice.");
            }

            if (invoice.RemainingAmount <= 0)
            {
                _logger.LogWarning("Invoice with id {InvoiceId}  is already fully paid.", invoiceId);
                return Result.Failure(
                    message: "Invoice is already fully paid.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            if (command.Amount > invoice.RemainingAmount)
            {
                _logger.LogWarning(
                    "Payment amount for invoice {InvoiceId} exceeds the remaining balance ({RemainingAmount}).",
                    invoiceId, invoice.RemainingAmount);

                return Result.Failure(
                    message: $"Payment amount exceeds the remaining balance ({invoice.RemainingAmount}).",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var payment = new InvoicePayment
            {
                InvoiceId = invoice.Id,
                Amount = command.Amount,
                PaymentDate = command.PaymentDate.ToUniversalTime(),
                ReferenceNumber = command.ReferenceNumber?.Trim(),
                Note = command.Note?.Trim(),
                CreatedById = userId
            };

            invoice.PaidAmount += command.Amount;

            if (invoice.PaidAmount >= invoice.TotalAmount)
            {
                invoice.PaymentDate = command.PaymentDate.ToUniversalTime();
            }

            _context.InvoicePayments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Payment {PaymentId} of amount {Amount} added to invoice {InvoiceId} by user {UserId}.",
                payment.Id, payment.Amount, invoice.Id, userId);

            return Result.Success(
                message: "Payment registered successfully.",
                statusCode: StatusCodes.Status201Created
                );
        }

        public async Task<Result<PdfFileResponse>> DownloadInvoicePdfAsync(Guid invoiceId, string language = "pl")
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(i => i.Currency)
                .Include(i => i.Company)
                .Include(i => i.InvoiceProducts)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, _ct);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice with id {InvoiceId} not found.", invoiceId);
                return Result<PdfFileResponse>.Failure(
                    message: "Invoice not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.InvoiceNotFound
                );
            }

            var lang = language.ToLower() == "en" ? "en" : "pl";
            var pdfBytes = _pdf.GenerateInvoicePdf(invoice, lang);

            var safeInvoiceNumber = invoice.InvoiceNumber.Replace("/", "_").Replace("\\", "_");
            var fileName = lang == "en"
                ? $"Invoice_{safeInvoiceNumber}.pdf"
                : $"Faktura_{safeInvoiceNumber}.pdf";

            var response = new PdfFileResponse
            {
                FileContents = pdfBytes,
                ContentType = "application/pdf",
                FileName = fileName
            };

            _logger.LogInformation("Generate pdf for invoice with id {invoiceId}", invoiceId);

            return Result<PdfFileResponse>.Success(
                message: "Invoice PDF generated successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<ProductInvoiceItemResponse>>> GetProductInvoicesAsync(Guid productId, SimpleListCommand command)
            => await _context.InvoiceProducts
                .Where(ip => ip.ProductId == productId)
                .AsNoTracking()
                .ApplyProductInvoiceSearch(command.SearchTerm)
                .OrderByDescending(ip => ip.Invoice.IssueDate)
                .Select(ip => new ProductInvoiceItemResponse
                {
                    InvoiceId = ip.InvoiceId,
                    InvoiceNumber = ip.Invoice.InvoiceNumber,
                    CompanyName = ip.Invoice.Company.Name,
                    IssueDate = ip.Invoice.IssueDate,
                    DueDate = ip.Invoice.DueDate,
                    Quantity = ip.Quantity,
                    UnitPrice = ip.UnitPrice,
                    TotalPrice = (long)ip.Quantity * ip.UnitPrice,
                    CurrencyCode = ip.Invoice.Currency.Code,
                    DecimalPlaces = ip.Invoice.Currency.DecimalPlaces,
                    IsPaid = ip.Invoice.PaidAmount >= ip.Invoice.TotalAmount
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "product_invoices", _ct);
    }
}
