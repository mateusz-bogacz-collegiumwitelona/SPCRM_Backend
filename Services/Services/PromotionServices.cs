using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Promotion;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Promotion;

namespace Services.Services
{
    public class PromotionServices : IPromotionServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PromotionServices> _logger;

        public PromotionServices(AppDbContext context, ILogger<PromotionServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<PromotionResponse>>> GetPromotionListAsync(PromotionListCommand command)
            => await _context.Promotions
                    .AsNoTracking()
                    .ApplyFilter(command)
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(p => new PromotionResponse
                    {
                        Id = p.Id,
                        Name = p.Name,
                        DiscountPercentage = p.DiscountPercentage,

                        PromotionalPrice = p.PromotionalPrice,

                        PromotionalPriceCode = p.Currency != null
                        ? p.Currency.Code
                        : null,

                        PromotionalPriceDecimalPlace = p.Currency != null
                        ? (int?)p.Currency.DecimalPlaces
                        : null,

                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        IsActive = p.IsActive
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "promotions");


        public async Task<Result<PromotionDetailResponse>> GetPromotionDetailAsync(Guid promotionId)
        {
            var rawData = await (
                from p in _context.Promotions.AsNoTracking()
                where p.Id == promotionId
                join pr in _context.Products.AsNoTracking() on p.ProductId equals pr.Id into prodGroup
                from pr in prodGroup.DefaultIfEmpty()
                join sg in _context.SteelGrades.AsNoTracking() on pr.SteelGradeId equals sg.Id into sgGroup
                from sg in sgGroup.DefaultIfEmpty()
                join u in _context.UnitsOfMeasure.AsNoTracking() on pr.UnitId equals u.Id into uGroup
                from u in uGroup.DefaultIfEmpty()
                join curr in _context.Currencies.AsNoTracking() on p.CurrencyId equals curr.Id into currGroup
                from curr in currGroup.DefaultIfEmpty()
                join c in _context.Contacts.AsNoTracking() on p.ContactId equals c.Id into cGroup
                from c in cGroup.DefaultIfEmpty()
                join comp in _context.Companies.AsNoTracking() on c.CompanyId equals comp.Id into compGroup
                from comp in compGroup.DefaultIfEmpty()
                select new
                {
                    p.Id,
                    p.Name,
                    p.IsActive,
                    p.StartDate,
                    p.EndDate,
                    p.DiscountPercentage,
                    p.PromotionalPrice,
                    p.CurrencyId,
                    CurrencyCode = curr != null ? curr.Code : null,
                    CurrencyDecimalPlaces = curr != null ? (int?)curr.DecimalPlaces : null,
                    p.MinQuantity,
                    p.MinWeight,

                    p.ProductId,
                    HasProduct = pr != null,
                    ProductName = pr != null ? pr.Name : null,
                    HasSteelGrade = sg != null,
                    SteelGradeName = sg != null ? sg.Name : null,
                    Category = pr != null ? (ProductCategoryEnum?)pr.Category : null,
                    Diameter = pr != null ? pr.Diameter : null,
                    Thickness = pr != null ? (int?)pr.Thickness : null,
                    Width = pr != null ? (int?)pr.Width : null,
                    Length = pr != null ? (int?)pr.Length : null,
                    PricePerUnit = pr != null ? (long?)pr.PricePerUnit : null,
                    StockQuantity = pr != null ? (int?)pr.StockQuantity : null,
                    UnitSymbol = u != null ? u.Symbol : null,

                    p.ContactId,
                    HasContact = c != null,
                    ContactFirstName = c != null ? c.FirstName : null,
                    ContactLastName = c != null ? c.LastName : null,
                    ContactCompanyName = comp != null ? comp.Name : null,

                    p.CreatedAt,
                    p.UpdateAt
                }
            ).FirstOrDefaultAsync();

            if (rawData == null)
            {
                _logger.LogInformation("Promotion with ID {PromotionId} not found.", promotionId);
                return Result<PromotionDetailResponse>.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            if (!rawData.HasProduct || !rawData.HasSteelGrade || string.IsNullOrWhiteSpace(rawData.UnitSymbol))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} has corrupted Product ({ProductId}) or missing dictionary linkages.",
                    promotionId, rawData.ProductId);
                throw new DataCorruptionException($"Promotion '{promotionId}' is linked to non-existent or corrupted product.");
            }

            if (rawData.ContactId.HasValue && (!rawData.HasContact || string.IsNullOrWhiteSpace(rawData.ContactFirstName)))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} is linked to non-existent Contact {ContactId}.",
                    promotionId, rawData.ContactId.Value);
                throw new DataCorruptionException($"Promotion '{promotionId}' has orphaned contact linkage.");
            }

            if (rawData.PromotionalPrice.HasValue)
            {
                if (rawData.PromotionalPrice.Value < 0 || rawData.CurrencyId == null || string.IsNullOrWhiteSpace(rawData.CurrencyCode))
                {
                    _logger.LogError("Critical data corruption: Promotion {PromotionId} has promotional price without valid currency.", promotionId);
                    throw new DataCorruptionException($"Promotion '{promotionId}' has invalid pricing or missing currency relation.");
                }
            }

            if (rawData.DiscountPercentage.HasValue && (rawData.DiscountPercentage.Value < 0 || rawData.DiscountPercentage.Value > 100))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} has invalid discount percentage ({Discount}%).",
                    promotionId, rawData.DiscountPercentage.Value);
                throw new DataCorruptionException($"Promotion '{promotionId}' contains corrupted discount percentage.");
            }

            var formattedDimensions = DimensionsFormatter.Format(
                rawData.Category!.Value,
                rawData.Diameter,
                rawData.Thickness!.Value,
                rawData.Width!.Value,
                rawData.Length!.Value
            );

            var response = new PromotionDetailResponse
            {
                Id = rawData.Id,
                Name = rawData.Name,
                IsActive = rawData.IsActive,
                StartDate = rawData.StartDate,
                EndDate = rawData.EndDate,
                DiscountPercentage = rawData.DiscountPercentage,
                PromotionalPrice = rawData.PromotionalPrice,
                CurrencyCode = rawData.CurrencyCode,
                CurrencyDecimalPlaces = rawData.CurrencyDecimalPlaces,
                MinQuantity = rawData.MinQuantity,
                MinWeight = rawData.MinWeight,

                ProductId = rawData.ProductId,
                ProductName = rawData.ProductName!,
                SteelGrade = rawData.SteelGradeName!,
                Category = rawData.Category.Value.ToString(),
                Dimensions = formattedDimensions,
                ProductPricePerUnit = rawData.PricePerUnit!.Value,
                ProductStockQuantity = rawData.StockQuantity!.Value,
                UnitSymbol = rawData.UnitSymbol,

                ContactId = rawData.ContactId,
                ContactFirstName = rawData.ContactFirstName,
                ContactLastName = rawData.ContactLastName,
                ContactCompanyName = rawData.ContactCompanyName,

                CreatedAt = rawData.CreatedAt,
                UpdateAt = rawData.UpdateAt
            };

            return Result<PromotionDetailResponse>.Success(
                message: "Promotion details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result> DeactivatePromotionAsync(Guid promotionId)
        {
            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId);

            if (promotion == null)
            {
                _logger.LogWarning("Promotion with id {promotionId} not found.", promotionId);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                    );
            }

            if (!promotion.IsActive)
            {
                return Result.Success(
                    message: "Promotion is already deactivated.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            promotion.IsActive = false;
            promotion.EndDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Promotion deactivate successfully",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result> ActivatePromotionAsync(ActivatePromotionCommand command)
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == command.Id);

            if (promotion == null)
            {
                _logger.LogInformation("Promotion with ID {PromotionId} not found.", command.Id);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            // 1. Sprawdzenie integralności encji promocji w bazie
            if (promotion.ProductId == Guid.Empty ||
                (promotion.PromotionalPrice.HasValue && (!promotion.CurrencyId.HasValue || promotion.PromotionalPrice.Value < 0)) ||
                (promotion.DiscountPercentage.HasValue && (promotion.DiscountPercentage.Value < 0 || promotion.DiscountPercentage.Value > 100)))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} has missing ProductId or invalid pricing/currency.", promotion.Id);
                throw new DataCorruptionException($"Promotion '{promotion.Id}' has corrupted pricing, currency, or product linkage.");
            }

            if (promotion.IsActive)
            {
                return Result.Success(
                    message: "Promotion is already activated.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            DateTime now = DateTime.UtcNow;

            DateTime? targetEndDate = null;
            if (command.EndDate != default)
            {
                var utcEndDate = DateTime.SpecifyKind(command.EndDate, DateTimeKind.Utc);
                if (utcEndDate <= now)
                {
                    _logger.LogWarning("Attempt to activate promotion {PromotionId} with an end date in the past: {EndDate}", promotion.Id, utcEndDate);
                    return Result.Failure(
                        message: "Promotion end date must be in the future.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidDate
                    );
                }
                targetEndDate = utcEndDate;
            }

            var hasActiveConflict = await _context.Promotions
                .AsNoTracking()
                .AnyAsync(p => p.ProductId == promotion.ProductId
                    && p.Id != promotion.Id
                    && p.IsActive
                    && p.ContactId == promotion.ContactId
                    && (!p.EndDate.HasValue || p.EndDate >= now));

            if (hasActiveConflict)
            {
                _logger.LogWarning("Product {ProductId} already has another active promotion for the same scope.", promotion.ProductId);
                return Result.Failure(
                    message: "This product already has another active promotion for the specified audience.",
                    statusCode: StatusCodes.Status409Conflict,
                    errorCode: ErrorCodes.ActivePromotionAlreadyExists
                );
            }

            promotion.IsActive = true;
            promotion.StartDate = now;
            promotion.EndDate = targetEndDate;
            promotion.UpdateAt = now;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Promotion {PromotionId} activated successfully for Product {ProductId}.", promotion.Id, promotion.ProductId);

            return Result.Success(
                message: "Promotion activated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeletePromotionAsync(Guid promotionId)
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == promotionId);

            if (promotion == null)
            {
                _logger.LogInformation("Promotion with ID {PromotionId} not found.", promotionId);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            if (promotion.ProductId == Guid.Empty ||
                (promotion.PromotionalPrice.HasValue && !promotion.CurrencyId.HasValue))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} has missing ProductId or missing currency for PromotionalPrice.", promotionId);
                throw new DataCorruptionException($"Promotion '{promotionId}' has corrupted product or currency linkage.");
            }

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Promotion {PromotionId} for Product {ProductId} deleted successfully.", promotion.Id, promotion.ProductId);

            return Result.Success(
                message: "Promotion deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditPromotionAsync(EditPromotionCommand command)
        {
            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == command.Id);

            if (promotion == null)
            {
                _logger.LogInformation("Promotion with ID {PromotionId} not found.", command.Id);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            if (promotion.ProductId == Guid.Empty ||
                (promotion.PromotionalPrice.HasValue && (!promotion.CurrencyId.HasValue || promotion.PromotionalPrice.Value < 0)) ||
                (promotion.DiscountPercentage.HasValue && (promotion.DiscountPercentage.Value < 0 || promotion.DiscountPercentage.Value > 100)))
            {
                _logger.LogError("Critical data corruption: Promotion {PromotionId} has missing ProductId or invalid pricing/currency.", promotion.Id);
                throw new DataCorruptionException($"Promotion '{promotion.Id}' contains corrupted state or broken product/currency linkage.");
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                promotion.Name = command.Name.Trim();
            }

            if (command.StartDate.HasValue)
            {
                promotion.StartDate = DateTime.SpecifyKind(command.StartDate.Value, DateTimeKind.Utc);
            }

            if (command.EndDate.HasValue)
            {
                var newEndDate = DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc);
                var effectiveStartDate = promotion.StartDate ?? promotion.CreatedAt;

                if (newEndDate < effectiveStartDate)
                {
                    _logger.LogWarning("The end date {EndDate} cannot be earlier than start date {StartDate} for promotion {PromotionId}.",
                        newEndDate, effectiveStartDate, command.Id);
                    return Result.Failure(
                        message: "The end date cannot be earlier than the start date of the promotion.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidDate
                    );
                }

                promotion.EndDate = newEndDate;
            }

            if (command.DiscountPercentage.HasValue)
            {
                promotion.DiscountPercentage = command.DiscountPercentage.Value;
                promotion.PromotionalPrice = null;
                promotion.CurrencyId = null;
            }
            else if (command.PromotionalPrice.HasValue)
            {
                if (command.CurrencyId.HasValue)
                {
                    var currencyExists = await _context.Currencies
                        .AsNoTracking()
                        .AnyAsync(c => c.Id == command.CurrencyId.Value);

                    if (!currencyExists)
                    {
                        _logger.LogWarning("Currency with ID {CurrencyId} not found.", command.CurrencyId.Value);
                        return Result.Failure(
                            message: "Currency not found.",
                            statusCode: StatusCodes.Status400BadRequest,
                            errorCode: ErrorCodes.CurrencyNotFound
                        );
                    }
                    promotion.CurrencyId = command.CurrencyId.Value;
                }
                else if (!promotion.CurrencyId.HasValue)
                {
                    _logger.LogWarning("Attempt to set PromotionalPrice for promotion {PromotionId} without specifying CurrencyId.", command.Id);
                    return Result.Failure(
                        message: "Currency must be specified when setting promotional price.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation
                    );
                }

                promotion.PromotionalPrice = command.PromotionalPrice.Value;
                promotion.DiscountPercentage = null;
            }

            if (command.ContactId.HasValue)
            {
                var contactExists = await _context.Contacts
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == command.ContactId.Value);

                if (!contactExists)
                {
                    _logger.LogWarning("Contact with ID {ContactId} not found.", command.ContactId.Value);
                    return Result.Failure(
                        message: "Contact not found.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.ContactNotFound
                    );
                }
                promotion.ContactId = command.ContactId.Value;
            }

            if (command.MinQuantity.HasValue)
            {
                promotion.MinQuantity = command.MinQuantity.Value;
            }

            if (command.MinWeight.HasValue)
            {
                promotion.MinWeight = command.MinWeight.Value;
            }

            promotion.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Promotion {PromotionId} updated successfully.", promotion.Id);

            return Result.Success(
                message: "Promotion updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> AddPromotionAsync(AddPromotionCommand command)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == command.ProductId)
                .Select(p => new
                {
                    p.Id,
                    p.PricePerUnit,
                    p.CurrencyId,
                    p.SteelGradeId,
                    p.UnitId
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                _logger.LogInformation("Product with ID {ProductId} not found.", command.ProductId);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (product.PricePerUnit < 0 || product.CurrencyId == Guid.Empty || product.SteelGradeId == Guid.Empty || product.UnitId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has corrupted pricing or dictionary linkages.", product.Id);
                throw new DataCorruptionException($"Product '{product.Id}' contains corrupted state.");
            }

            if (command.PromotionalPrice.HasValue)
            {
                if (!command.CurrencyId.HasValue)
                {
                    _logger.LogWarning("Attempt to create promotion with PromotionalPrice without specifying CurrencyId.");
                    return Result.Failure(
                        message: "Currency must be specified when setting promotional price.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation
                    );
                }

                var currencyExists = await _context.Currencies
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == command.CurrencyId.Value);

                if (!currencyExists)
                {
                    _logger.LogWarning("Currency with ID {CurrencyId} not found.", command.CurrencyId.Value);
                    return Result.Failure(
                        message: "Currency not found.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.CurrencyNotFound
                    );
                }
            }

            if (command.ContactId.HasValue)
            {
                var contactExists = await _context.Contacts
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == command.ContactId.Value);

                if (!contactExists)
                {
                    _logger.LogWarning("Contact with ID {ContactId} not found.", command.ContactId.Value);
                    return Result.Failure(
                        message: "Contact not found.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.ContactNotFound
                    );
                }
            }

            var now = DateTime.UtcNow;
            var startDate = command.StartDate.HasValue
                ? DateTime.SpecifyKind(command.StartDate.Value, DateTimeKind.Utc)
                : now;

            DateTime? endDate = null;
            if (command.EndDate.HasValue)
            {
                var utcEndDate = DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc);
                if (utcEndDate < startDate)
                {
                    _logger.LogWarning("Promotion end date {EndDate} is earlier than start date {StartDate}.", utcEndDate, startDate);
                    return Result.Failure(
                        message: "The end date cannot be earlier than the start date.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidDate
                    );
                }
                endDate = utcEndDate;
            }

            var hasConflict = await _context.Promotions
                .AsNoTracking()
                .AnyAsync(p =>
                    p.ProductId == command.ProductId &&
                    p.IsActive &&
                    p.ContactId == command.ContactId &&
                    (!p.EndDate.HasValue || p.EndDate.Value > now)
                );

            if (hasConflict)
            {
                _logger.LogWarning("Active promotion collision detected for Product {ProductId} and Contact scope {ContactId}.",
                    command.ProductId, command.ContactId);
                return Result.Failure(
                    message: "An active promotion for this product already exists for the specified audience.",
                    statusCode: StatusCodes.Status409Conflict,
                    errorCode: ErrorCodes.ActivePromotionAlreadyExists
                );
            }

            var promotion = new Promotion
            {
                Id = Guid.NewGuid(),
                Name = command.Name.Trim(),
                ProductId = command.ProductId,
                StartDate = startDate,
                EndDate = endDate,
                DiscountPercentage = command.DiscountPercentage,
                PromotionalPrice = command.PromotionalPrice,
                CurrencyId = command.PromotionalPrice.HasValue ? command.CurrencyId : null,
                ContactId = command.ContactId,
                MinQuantity = command.MinQuantity,
                MinWeight = command.MinWeight,
                IsActive = true
            };

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Promotion {PromotionId} ('{PromotionName}') added successfully for Product {ProductId}.",
                promotion.Id, promotion.Name, promotion.ProductId);

            return Result.Success(
                message: "Promotion added successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }
    }
}
