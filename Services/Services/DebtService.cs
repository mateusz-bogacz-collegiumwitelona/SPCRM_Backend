using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Company;
using Services.Helpers;
using Services.Interfaces;
using Services.Response.Company;


namespace Services.Services
{
    public class DebtService : IDebtService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DebtService> _logger;

        public DebtService(AppDbContext context, ILogger<DebtService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid companyId)
        {
            var companyExists = await _context.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == companyId);

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
                .ToListAsync();

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
                .AnyAsync(c => c.Id == command.CompanyId);

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
                .AnyAsync(i => i.CurrencyId == Guid.Empty || i.PaidAmount < 0 || i.TotalAmount < 0);

            if (hasCorruptedInvoices)
            {
                _logger.LogError("Critical data corruption: Found invoices with invalid currency or negative amounts for company {CompanyId}.", command.CompanyId);
                throw new DataCorruptionException($"Company '{command.CompanyId}' contains corrupted invoices.");
            }

            var now = DateTime.UtcNow;

            var query = _context.Invoices
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
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_debt");
        }
    }
}
