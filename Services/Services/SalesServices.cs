using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Company;
using Services.Command.Product;
using Services.Command.Sales;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;
using Services.Response.Deal;
using Services.Response.Sale;

namespace Services.Services
{
    public class SalesServices : ISalesServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SalesServices> _logger;

        public SalesServices(
            AppDbContext context,
            ILogger<SalesServices> logger
            )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, SalesListCommand command)
            => await _context.Deals
                    .AsNoTracking()
                    .Include(d => d.Company)
                    .Include(d => d.Currency)
                    .Where(d => d.OwnerId == userId)
                    .ApplyFilter(
                        command.CompanyName,
                        command.Value,
                        command.DateFrom,
                        command.DateTo,
                        command.StatusType
                    )
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .Select(d => new UserSalesResponse
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Nip = d.Company.NIP,
                        CloseDate = d.CloseDate,
                        Value = d.Value,
                        DecimalPlace = d.Currency.DecimalPlaces,
                        Currency = d.Currency.Name,
                        CompanyName = d.Company.Name,
                        Status = d.Status.ToString()
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "sales");

        public async Task<Result<List<string>>> GetSalesStatus()
            => Result<List<string>>.Success(
                message: "Sales statuses retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames(typeof(DealsStatusEnum)).ToList()
                );

        public async Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(CompanyCommand command)
            => await _context.Deals
                    .Where(d => d.CompanyId == command.CompanyId)
                    .Select(d => new CompanySalesResponse
                    {
                        Id = d.Id,
                        SalesmanFirstName = d.Owner.FirstName,
                        SalesmanLastName = d.Owner.LastName,
                        Name = d.Name,
                        Value = (decimal)d.Value / 10000m,
                        Code = d.Currency.Code,
                        DecimalPlaces = d.Currency.DecimalPlaces,
                        Status = d.Status.ToString(),
                        CloseDate = d.CloseDate,
                        CreatedAt = d.CreatedAt
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_sales");

        public async Task<Result<SaleDetailResponse>> GetSaleDetailAsync(Guid dealId)
        {
            var now = DateTime.UtcNow;

            var query = await (
                from d in _context.Deals.AsNoTracking()
                where d.Id == dealId
                join curr in _context.Currencies.AsNoTracking() on d.CurrencyId equals curr.Id into currGroup
                from curr in currGroup.DefaultIfEmpty()
                join u in _context.Users.AsNoTracking() on d.OwnerId equals u.Id into uGroup
                from u in uGroup.DefaultIfEmpty()
                join comp in _context.Companies.AsNoTracking() on d.CompanyId equals comp.Id into compGroup
                from comp in compGroup.DefaultIfEmpty()
                select new
                {
                    DealExists = true,
                    d.Id,
                    d.Name,
                    d.Value,
                    Status = d.Status.ToString(),
                    d.CloseDate,

                    d.CurrencyId,
                    HasCurrency = curr != null,
                    CurrencyCode = curr != null ? curr.Code : null,
                    DecimalPlaces = curr != null ? (int?)curr.DecimalPlaces : null,

                    d.OwnerId,
                    HasOwner = u != null,
                    OwnerFirstName = u != null ? u.FirstName : null,
                    OwnerLastName = u != null ? u.LastName : null,

                    d.CompanyId,
                    HasCompany = comp != null,
                    CompanyName = comp != null ? comp.Name : null,

                    InvoicedAmount = d.Invoices.Sum(i => (long?)i.TotalAmount) ?? 0,
                    PaidAmount = d.Invoices.Sum(i => (long?)i.PaidAmount) ?? 0,
                    IsOverdueInvoices = d.Invoices.Any(i =>
                        (i.TotalAmount - i.PaidAmount) > 0 &&
                        i.DueDate < now
                    )
                }
            ).FirstOrDefaultAsync();

            if (query == null)
            {
                _logger.LogInformation("Sale with ID {DealId} not found.", dealId);
                return Result<SaleDetailResponse>.Failure(
                    message: "Sale not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            if (!query.HasCurrency || !query.HasOwner || !query.HasCompany ||
                string.IsNullOrWhiteSpace(query.CurrencyCode) ||
                !query.DecimalPlaces.HasValue)
            {
                _logger.LogError("Critical data corruption: Deal {DealId} has missing Currency, Owner, or Company linkage.", dealId);
                throw new DataCorruptionException($"Deal '{dealId}' contains corrupted relational linkages.");
            }

            if (query.Value < 0 || query.InvoicedAmount < 0 || query.PaidAmount < 0)
            {
                _logger.LogError("Critical data corruption: Deal {DealId} contains negative monetary values (Value: {Value}, Invoiced: {Invoiced}, Paid: {Paid}).",
                    dealId, query.Value, query.InvoicedAmount, query.PaidAmount);
                throw new DataCorruptionException($"Deal '{dealId}' contains corrupted financial amounts.");
            }

            var paymentPercentage = query.Value > 0
                ? (int)Math.Round(
                    (decimal)query.PaidAmount / query.Value * 100m,
                    MidpointRounding.AwayFromZero)
                : 0;

            var response = new SaleDetailResponse
            {
                Id = query.Id,
                Name = query.Name,
                Value = query.Value,
                Status = query.Status,
                CloseDate = query.CloseDate,
                CurrencyCode = query.CurrencyCode,
                DecimalPlaces = query.DecimalPlaces.Value,
                OwnerFirstName = query.OwnerFirstName ?? string.Empty,
                OwnerLastName = query.OwnerLastName ?? string.Empty,
                CompanyName = query.CompanyName ?? string.Empty,
                InvoicedAmount = query.InvoicedAmount,
                PaidAmount = query.PaidAmount,
                IsOverduelInvoices = query.IsOverdueInvoices,
                PaymentPercentage = paymentPercentage
            };

            return Result<SaleDetailResponse>.Success(
                message: "Sale detail retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(Guid dealId, ProductListCommand command)
            => await _context.DealProducts
                    .AsNoTracking()
                    .Where(dp => dp.DealId == dealId)
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .ApplyFilter(command.ProductCategory, command.SteelGrade)
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(dp => new DealProductResponse
                    {
                        ProductId = dp.ProductId,
                        Name = dp.Product.Name,
                        SteelGrade = dp.Product.SteelGrade.Name,

                        Dimensions = DimensionsFormatter.Format(
                        dp.Product.Category,
                        dp.Product.Diameter,
                        dp.Product.Thickness,
                        dp.Product.Width,
                        dp.Product.Length
                    ),

                        Quantity = dp.Quantity,
                        UnitSymbol = dp.Product.Unit.Symbol,
                        BaseUnitPrice = dp.Product.PricePerUnit,
                        UnitPrice = dp.UnitPrice,
                        TotalPrice = dp.Quantity * dp.UnitPrice,
                        CurrencyCode = dp.Deal.Currency.Code,
                        DecimalPlaces = dp.Deal.Currency.DecimalPlaces
                    }).ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "deal_products");
    }
}
