using Domain.Common;
using Domain.Constants;
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
            var rawData = await _context.Promotions
                .AsNoTracking()
                .Where(p => p.Id == promotionId)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.IsActive,
                    p.StartDate,
                    p.EndDate,
                    p.DiscountPercentage,
                    p.PromotionalPrice,
                    CurrencyCode = p.Currency != null ? p.Currency.Code : null,
                    CurrencyDecimalPlaces = p.Currency != null ? (int?)p.Currency.DecimalPlaces : null,
                    p.MinQuantity,
                    p.MinWeight,

                    ProductId = p.Product.Id,
                    ProductName = p.Product.Name,
                    p.Product.SteelGrade,
                    p.Product.Category,
                    p.Product.Diameter,
                    p.Product.Thickness,
                    p.Product.Width,
                    p.Product.Length,
                    p.Product.PricePerUnit,
                    p.Product.StockQuantity,
                    UnitSymbol = p.Product.Unit != null ? p.Product.Unit.Symbol : "szt.",

                    p.ContactId,
                    ContactFirstName = p.Contact != null ? p.Contact.FirstName : null,
                    ContactLastName = p.Contact != null ? p.Contact.LastName : null,
                    ContactCompanyName = p.Contact != null && p.Contact.Company != null ? p.Contact.Company.Name : null,

                    p.CreatedAt,
                    p.UpdateAt
                })
                .FirstOrDefaultAsync();

            if (rawData == null)
            {
                _logger.LogWarning("Promotion with ID {PromotionId} not found.", promotionId);
                return Result<PromotionDetailResponse>.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            var formattedDimensions = DimensionsFormatter.Format(
                rawData.Category,
                rawData.Diameter,
                rawData.Thickness,
                rawData.Width,
                rawData.Length
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
                ProductName = rawData.ProductName,
                SteelGrade = rawData.SteelGrade.Name,
                Category = rawData.Category.ToString(),
                Dimensions = formattedDimensions,
                ProductPricePerUnit = rawData.PricePerUnit,
                ProductStockQuantity = rawData.StockQuantity,
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
            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == command.Id);

            if (promotion == null)
            {
                _logger.LogWarning("Promotion with id {promotionId} not found.", command.Id);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                    );
            }

            if (promotion.IsActive)
            {
                return Result.Success(
                    message: "Promotion is already activated.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            DateTime now = DateTime.UtcNow;

            var hasActivePromotion = await _context.Promotions
                .AnyAsync(p => p.ProductId == promotion.ProductId
                    && p.Id != promotion.Id
                    && p.IsActive
                    && (!p.EndDate.HasValue || p.EndDate >= now));

            if (hasActivePromotion)
            {
                _logger.LogWarning("Product with id {productId} already has another active promotion.", promotion.ProductId);
                return Result.Failure(
                    message: "This product already has another active promotion.",
                    statusCode: StatusCodes.Status409Conflict,
                    errorCode: ErrorCodes.ActivePromotionAlreadyExists
                );
            }

            promotion.IsActive = true;
            promotion.StartDate = now;
            promotion.EndDate = command.EndDate;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Promotion activate successfully",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result> DeletePromotionAsync(Guid promotionId)
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

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();


            return Result.Success(
                message: "Promotion deleted successfully",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result> EditPromotionAsync(EditPromotionCommand command)
        {
            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == command.Id);

            if (promotion == null)
            {
                _logger.LogWarning("Promotion with ID {PromotionId} not found.", command.Id);
                return Result.Failure(
                    message: "Promotion not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.PromotionNotFound
                );
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                promotion.Name = command.Name.Trim();
            }

            if (command.StartDate.HasValue)
            {
                promotion.StartDate = command.StartDate.Value.ToUniversalTime();
            }

            if (command.EndDate.HasValue)
            {
                var newEndDate = command.EndDate.Value.ToUniversalTime();
                var effectiveStartDate = promotion.StartDate ?? promotion.CreatedAt;

                if (newEndDate < effectiveStartDate)
                {
                    _logger.LogError("The end date {EndDate} cannot be earlier than the start date {StartDate} of the promotion with ID {PromotionId}.", newEndDate, effectiveStartDate, command.Id);
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

            if (command.PromotionalPrice.HasValue)
            {
                if (command.CurrencyId.HasValue)
                {
                    var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == command.CurrencyId.Value);
                    if (!currencyExists)
                    {
                        return Result.Failure(
                            message: "Currency not found.",
                            statusCode: StatusCodes.Status400BadRequest,
                            errorCode: ErrorCodes.CurrencyNotFound
                        );
                    }
                    promotion.CurrencyId = command.CurrencyId.Value;
                }

                promotion.PromotionalPrice = command.PromotionalPrice.Value;
                promotion.DiscountPercentage = null;
            }

            if (command.ContactId.HasValue)
            {
                var contactExists = await _context.Contacts.AnyAsync(c => c.Id == command.ContactId.Value);
                if (!contactExists)
                {
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

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Promotion updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> AddPromotionAsync(AddPromotionCommand command)
        {
            var hasProductExitst = await _context.Products.AnyAsync(p => p.Id == command.ProductId);

            if (!hasProductExitst)
            {
                _logger.LogWarning("Product with id {productId} not found.", command.ProductId);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (command.CurrencyId.HasValue)
            {
                var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == command.CurrencyId.Value);
                if (!currencyExists)
                {
                    _logger.LogWarning("Currency with id {currencyId} not found.", command.CurrencyId.Value);
                    return Result.Failure(
                        message: "Currency not found.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.CurrencyNotFound
                    );
                }
            }

            if (command.ContactId.HasValue)
            {
                var contactExists = await _context.Contacts.AnyAsync(c => c.Id == command.ContactId.Value);
                if (!contactExists)
                {
                    _logger.LogWarning("Contact with id {contactId} not found.", command.ContactId.Value);
                    return Result.Failure(
                        message: "Contact not found.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.ContactNotFound
                    );
                }
            }

            var now = DateTime.UtcNow;
            var startDate = command.StartDate?.ToUniversalTime() ?? now;
            var endDate = command.EndDate?.ToUniversalTime();

            var hasConflict = await _context.Promotions.AnyAsync(p =>
                p.ProductId == command.ProductId &&
                p.IsActive &&
                (!p.EndDate.HasValue || p.EndDate.Value > now)
            );

            if (hasConflict)
            {
                return Result.Failure(
                    message: "An active promotion for this product already exists.",
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

            return Result.Success(
                message: "Promotion added successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }
    }
}
