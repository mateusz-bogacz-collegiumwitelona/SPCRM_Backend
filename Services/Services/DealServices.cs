using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Company;
using Services.Command.Deal;
using Services.Command.Product;
using Services.Command.Sales;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;
using Services.Response.Deal;

namespace Services.Services
{
    public class DealServices : IDealServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DealServices> _logger;
        private readonly IEntityAuthorizationService _entityAuth;

        public DealServices(
            AppDbContext context,
            ILogger<DealServices> logger,
            IEntityAuthorizationService entityAuth
            )
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
        }

        public async Task<Result<PagedResult<UserDealResponse>>> GetDealsAsync(DealListCommand command, Guid? forcedOwnerId = null)
        {
            var effectiveOwnerId = forcedOwnerId ?? command.OwnerId;

            var query = await _context.Deals
                       .AsNoTracking()
                       .ApplyFilter(
                           command.CompanyName,
                           command.Value,
                           command.DateFrom,
                           command.DateTo,
                           command.StatusType,
                           effectiveOwnerId
                       )
                       .ApplySorting(command.SortBy, command.SortDescending)
                       .ApplySearch(command.SearchTerm ?? string.Empty)
                       .Select(d => new UserDealResponse
                       {
                           Id = d.Id,
                           Name = d.Name,
                           Nip = d.Company.NIP,
                           CompanyName = d.Company.Name,
                           CloseDate = d.CloseDate,
                           Value = d.Value,
                           DecimalPlace = d.Currency.DecimalPlaces,
                           Currency = d.Currency.Code,
                           Status = d.Status.ToString(),
                           OwnerId = d.OwnerId,
                           OwnerFirstName = d.Owner.FirstName,
                           OwnerLastName = d.Owner.LastName
                       })
                       .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "sales");

            return query;
        }

        public async Task<Result<List<string>>> GetDealsStatus()
            => Result<List<string>>.Success(
                message: "Deals statuses retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames(typeof(DealsStatusEnum)).ToList()
                );

        public async Task<Result<PagedResult<CompanyDealsResponse>>> GetComapanyDealsAsync(CompanyCommand command)
            => await _context.Deals
                    .Where(d => d.CompanyId == command.CompanyId)
                    .Select(d => new CompanyDealsResponse
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

        public async Task<Result<DealDetailResponse>> GetDealDetailAsync(Guid dealId, Guid currentUserId)
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
                _logger.LogInformation("Deal with ID {DealId} not found.", dealId);
                return Result<DealDetailResponse>.Failure(
                    message: "Deal not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, query.OwnerId);

            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You are not authorized to modify this company.");
            }

            if (!query.HasCurrency || !query.HasOwner || !query.HasCompany ||
                string.IsNullOrWhiteSpace(query.CurrencyCode) ||
                !query.DecimalPlaces.HasValue)
            {
                _logger.LogError("Critical data corruption: Sale {DealId} has missing Currency, Owner, or Company linkage.", dealId);
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

            var response = new DealDetailResponse
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

            return Result<DealDetailResponse>.Success(
                message: "Deal detail retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(
            Guid dealId,
            ProductListCommand command,
            Guid currentUserId)
        {
            var dealOwnerId = await _context.Deals
                .AsNoTracking()
                .Where(d => d.Id == dealId)
                .Select(d => (Guid?)d.OwnerId)
                .FirstOrDefaultAsync();

            if (!dealOwnerId.HasValue)
            {
                _logger.LogInformation("Sale with ID {DealId} not found when fetching products.", dealId);
                return Result<PagedResult<DealProductResponse>>.Failure(
                    message: "Sale not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, dealOwnerId.Value);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to products of Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You don't have access to this");
            }

            return await _context.DealProducts
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
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "deal_products");
        }

        public async Task<Result> AddDealAsync(AddDealCommand command, Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found when attempting to add a deal.", userId);
                throw new UserNotFoundException(userId);
            }

            var company = await _context.Companies.FindAsync(command.CompanyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID {CompanyId} not found when attempting to add a deal.", command.CompanyId);
                return Result<Guid>.Failure(
                    message: "Company does not exist.",
                    errorCode: ErrorCodes.CompanyNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == command.CurrencyId);
            if (!currencyExists)
            {
                _logger.LogWarning("Currency with ID {CurrencyId} not found when attempting to add a deal.", command.CurrencyId);
                return Result<Guid>.Failure(
                    message: "Currency does not exist.",
                    errorCode: ErrorCodes.CurrencyNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (command.Products == null || !command.Products.Any())
            {
                return Result<Guid>.Failure(
                    message: "Deal must contain at least one product.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var hasInvalidItems = command.Products.Any(p => p.Quantity <= 0 || p.UnitPrice < 0 || p.ProductId == Guid.Empty);
            if (hasInvalidItems)
            {
                return Result<Guid>.Failure(
                    message: "All products must have positive quantity and non-negative unit price.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var requestedProductIds = command.Products.Select(p => p.ProductId).Distinct().ToList();
            var existingProductIds = await _context.Products
                .AsNoTracking()
                .Where(p => requestedProductIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            var missingProducts = requestedProductIds.Except(existingProductIds).ToList();
            if (missingProducts.Any())
            {
                return Result<Guid>.Failure(
                    message: "One or more products specified do not exist.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound,
                    errors: missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
            }

            var totalValue = command.Products.Sum(p => (long)p.Quantity * p.UnitPrice);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var countToday = await _context.Deals
                    .CountAsync(d => d.CreatedAt >= today && d.CreatedAt < tomorrow);

                var dealName = $"D/{today:yyyy/MM/dd}/{(countToday + 1):D4}";

                var deal = new Deal
                {
                    Name = dealName,
                    Value = totalValue,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = DateTime.SpecifyKind(command.CloseDate, DateTimeKind.Utc),
                    CurrencyId = command.CurrencyId,
                    CompanyId = command.CompanyId,
                    OwnerId = userId,
                    DealProducts = command.Products.Select(p => new DealProduct
                    {
                        Id = Guid.NewGuid(),
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,
                        UnitPrice = p.UnitPrice
                    }).ToList()
                };

                _context.Deals.Add(deal);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("New deal with ID {DealId} added successfully by user {UserId}.", deal.Id, userId);

                return Result.Success(
                    message: "Deal added successfully.",
                    statusCode: StatusCodes.Status201Created
                );
            }
            catch
            {
                await transaction.RollbackAsync();
                _logger.LogError("An error occurred while adding a new deal. Transaction rolled back.");
                throw;
            }
        }
    }
}
