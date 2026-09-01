using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.List;
using Services.Command.Offer;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Offer;

namespace Services.Services
{
    public class OfferServices : IOfferServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OfferServices> _logger;

        public OfferServices(AppDbContext context, ILogger<OfferServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<OfferListResponse>>> GetOfferListAsync(OfferListCommand command)
            => await _context.Offers
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplyFilter(
                    command.ValidUntilFrom,
                    command.ValidUntilTo,
                    command.CompanyName,
                    command.Status,
                    command.IsExpired
                )
                .ApplySorting(command.SortBy ?? string.Empty, command.SortDescending)
                .Select(o => new OfferListResponse
                {
                    OfferId = o.Id,
                    OfferName = o.Name,
                    ContactFirstName = o.Contact.FirstName,
                    ContactLastName = o.Contact.LastName,
                    CompanyName = o.Contact.Company.Name,
                    ValidUntil = o.ValidUntil,
                    Status = o.Status.ToString(),
                    IsExpired = o.ValidUntil < DateTime.UtcNow
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "offers");

        public async Task<Result<OfferDetailResponse>> GetOfferDetailAsync(Guid id)
        {
            var offer = await _context.Offers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", id);
                return Result<OfferDetailResponse>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var createdBy = await _context.Users.FirstOrDefaultAsync(u => u.Id == offer.CreatedByUserId);

            var response = new OfferDetailResponse
            {
                OfferId = offer.Id,
                OfferName = offer.Name,
                Status = offer.Status.ToString(),
                ValidUntil = offer.ValidUntil,
                IsExpired = offer.ValidUntil < DateTime.UtcNow,
                CreatedByUserFirstName = createdBy?.FirstName ?? string.Empty,
                CreatedByUserLastName = createdBy?.LastName ?? string.Empty
            };

            return Result<OfferDetailResponse>.Success(
                message: "Offer details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<OfferClientDetail>> GetOfferClientDetailAsync(Guid id)
        {
            var offer = await _context.Offers
                .AsNoTracking()
                .Include(o => o.Contact)
                    .ThenInclude(c => c.Company)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", id);
                return Result<OfferClientDetail>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var response = new OfferClientDetail
            {
                ContactId = offer.ContactId,
                ContactFirstName = offer.Contact.FirstName,
                ContactLastName = offer.Contact.LastName,
                ContactJobTitle = offer.Contact.JobTitle ?? string.Empty,
                CompanyName = offer.Contact.Company.Name
            };

            return Result<OfferClientDetail>.Success(
                message: "Offer client details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<OfferProductResponse>>> GetOfferProductsAsync(
            Guid id,
            SimpleListCommand command)
        {
            var offer = await _context.Offers
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", id);
                return Result<PagedResult<OfferProductResponse>>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return await _context.OfferProducts
                .AsNoTracking()
                .Where(op => op.OfferId == id)
                .ApplyProductSearch(command.SearchTerm ?? string.Empty)
                .OrderBy(op => op.Product.Name)
                .Select(op => new OfferProductResponse
                {
                    ProductId = op.ProductId,
                    ProductName = op.Product.Name,
                    SteelGrade = op.Product.SteelGrade.Name,
                    Quantity = op.Quantity,
                    QuotedPrice = op.QuotedPrice,
                    CurrencyCode = op.Offer.Currency.Code,
                    DecimalPlaces = op.Offer.Currency.DecimalPlaces
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "offer-products");
        }

        public async Task<Result> ExtendOfferValidityAsync(ExtendOfferValidityCommand command)
        {
            var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == command.OfferId);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (offer.Status == OfferStatusEnum.Accepted || offer.Status == OfferStatusEnum.Rejected)
            {
                return Result.Failure(
                    message: "Cannot extend validity of an accepted or rejected offer.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var targetDate = command.NewValidUntil.HasValue
                ? DateTime.SpecifyKind(command.NewValidUntil.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(7);

            if (targetDate <= DateTime.UtcNow)
            {
                return Result.Failure(
                    message: "New validity date must be in the future.",
                    errorCode: ErrorCodes.InvalidDate,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            offer.ValidUntil = targetDate;

            if (offer.Status == OfferStatusEnum.Expired)
            {
                offer.Status = OfferStatusEnum.Sent;
            }

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Offer validity extended successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result<Guid?>> ChangeOfferStatusAsync(ChangeOfferStatusCommand command)
        {
            var offer = await _context.Offers
                .Include(o => o.Contact)
                .Include(o => o.Currency)
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.Id == command.OfferId);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", command.OfferId);
                return Result<Guid?>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (offer.Status != OfferStatusEnum.Sent)
            {
                return Result<Guid?>.Failure(
                    message: $"Cannot change status of an offer with status '{offer.Status}'. Only 'Sent' offers can be modified.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (command.NewStatus != OfferStatusEnum.Accepted && command.NewStatus != OfferStatusEnum.Rejected)
            {
                return Result<Guid?>.Failure(
                    message: "Invalid target status. Status can only be changed to 'Accepted' or 'Rejected'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (offer.ValidUntil < DateTime.UtcNow)
            {
                offer.Status = OfferStatusEnum.Expired;
                await _context.SaveChangesAsync();

                return Result<Guid?>.Failure(
                    message: "Offer has expired and cannot be accepted or rejected without extending validity.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                offer.Status = command.NewStatus;
                Guid? createdDealId = null;

                if (command.NewStatus == OfferStatusEnum.Accepted)
                {
                    if (!offer.Products.Any())
                    {
                        return Result<Guid?>.Failure(
                            message: "Cannot accept an offer without any products.",
                            errorCode: ErrorCodes.InvalidOperation,
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    var totalValue = offer.Products.Sum(p => (long)p.Quantity * p.QuotedPrice);

                    var deal = new Deal
                    {
                        Id = Guid.NewGuid(),
                        Name = $"SE/{offer.Name}",
                        Value = totalValue,
                        Status = DealsStatusEnum.ToDo,
                        CloseDate = DateTime.UtcNow.AddMonths(1),
                        CurrencyId = offer.CurrencyId,
                        OwnerId = offer.CreatedByUserId,
                        CompanyId = offer.Contact.CompanyId,
                        DealProducts = offer.Products.Select(op => new DealProduct
                        {
                            Id = Guid.NewGuid(),
                            ProductId = op.ProductId,
                            Quantity = op.Quantity,
                            UnitPrice = op.QuotedPrice
                        }).ToList()
                    };

                    _context.Deals.Add(deal);
                    createdDealId = deal.Id;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result<Guid?>.Success(
                    message: command.NewStatus == OfferStatusEnum.Accepted
                        ? "Offer accepted and converted to sale deal successfully."
                        : "Offer rejected successfully.",
                    statusCode: StatusCodes.Status200OK,
                    data: createdDealId
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error while changing offer status for ID {OfferId}", command.OfferId);
                throw;
            }
        }
    }
}
