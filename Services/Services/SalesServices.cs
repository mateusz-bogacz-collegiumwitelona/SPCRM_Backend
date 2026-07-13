using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using DTO.Request;
using DTO.Response;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Interfaces;

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

        public async Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(
            Guid userId,
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            SalesFilterRequest filter
            )
        {
            var query = _context.Deals
                .Where(d => d.OwnerId == userId)
                .ApplyFilter(filter)
                .Select(d => new UserSalesResponse
                {
                    Id = d.Id,
                    Name = d.Name,
                    Nip = d.Company.NIP,
                    CloseDate = d.CloseDate,
                    Value = (decimal)d.Value / 10000m,
                    DecimalPlace = d.Currency.DecimalPlaces,
                    Currency = d.Currency.Name,
                    CompanyName = d.Company.Name,
                    Status = d.Status.ToString()
                })
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .ApplySorting(sorting);


            return await query.ToPagedResultAsync(pagged, _logger, "sales");
        }

        public async Task<Result<List<String>>> GetSalesStatus()
        {
            var statuses = Enum.GetNames(typeof(DealsStatusEnum)).ToList();

            return Result<List<string>>.Success(
                message: "Sales statuses retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: statuses
                );
        }

        public async Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(Guid comapnyId, PaggedRequest pagged)
        {
            var query = _context.Deals
                .Where(d => d.CompanyId == comapnyId)
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
                });

            return await query.ToPagedResultAsync(pagged, _logger, "company_sales");
        }

        public async Task<Result<SaleDetailResponse>> GetSaleDetailAsync(Guid dealId)
        {
            var query = await _context.Deals
                .Where(d => d.Id == dealId)
                .AsNoTracking()
                .Select(d => new 
                {
                    Id = d.Id,
                    Name = d.Name,
                    Value = d.Value,
                    Status = d.Status.ToString(),
                    CloseDate = d.CloseDate,
                    CurrencyCode = d.Currency.Code,
                    DecimalPlaces = d.Currency.DecimalPlaces,
                    OwnerFirstName = d.Owner.FirstName,
                    OwnerLastName = d.Owner.LastName,
                    CompanyName = d.Company.Name,
                    InvoicedAmount = d.Invoices.Sum(i => i.TotalAmount),
                    PaidAmount = d.Invoices.Sum(i => i.PaidAmount),
                    IsOverduelInvoices = d.Invoices.Any(i => 
                        (i.TotalAmount - i.PaidAmount) > 0 
                        && i.DueDate < DateTime.UtcNow
                    ),
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                _logger.LogWarning("Sale with ID {DealId} not found", dealId);
                return Result<SaleDetailResponse>.Failure(
                    message: "Sale not found",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound
                    );
            }

            var paymentPercentage = query.Value > 0
                ? (int)Math.Round(
                    (decimal)query.PaidAmount / (decimal)query.Value * 100m,
                    MidpointRounding.AwayFromZero)
                : 0;

            SaleDetailResponse response = new SaleDetailResponse 
            {
                Id = query.Id,
                Name = query.Name,
                Value = query.Value,
                Status = query.Status,
                CloseDate = query.CloseDate,
                CurrencyCode = query.CurrencyCode,
                DecimalPlaces = query.DecimalPlaces,
                OwnerFirstName = query.OwnerFirstName,
                OwnerLastName = query.OwnerLastName,
                CompanyName = query.CompanyName,
                InvoicedAmount = query.InvoicedAmount,
                PaidAmount = query.PaidAmount,
                IsOverduelInvoices = query.IsOverduelInvoices,
                PaymentPercentage = paymentPercentage,
            };

            return Result<SaleDetailResponse>.Success(
                message: "Sale detail retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
                );
        }

        public async Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(
            Guid dealId,
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
        )
        {
            var query = _context.DealProducts
                .AsNoTracking()
                .Include(dp => dp.Product)
                    .ThenInclude(p => p.ProductCategory)
                .Include(dp => dp.Product)
                    .ThenInclude(p => p.Unit)
                .Include(dp => dp.Deal)
                    .ThenInclude(d => d.Currency)
                .Where(dp => dp.DealId == dealId);

            query = query
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .ApplyFilter(filter)
                .ApplySorting(sorting);

            var pagedEntitiesResult = await query.ToPagedResultAsync(pagged, _logger, "deal_products");

            return pagedEntitiesResult.MapData(dp => new DealProductResponse
            {
                ProductId = dp.ProductId,
                Name = dp.Product.Name,
                SteelGrade = dp.Product.SteelGrade,

                Dimensions = DimensionsFormatter.Format(
                    dp.Product.ProductCategory.Category,
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
            });
        }

        public async Task<Result<List<NoteResponse>>> GetDealNotesAsync(Guid dealId)
        {
            var query = await _context.Notes
                .OfType<DealNote>()
                .AsNoTracking()
                .Where(n => n.DealId == dealId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NoteResponse
                {
                    NoteId = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdateAt ?? null,
                })
                .ToListAsync();

            return Result<List<NoteResponse>>.Success(
                data: query,
                message: "Deal notes retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
