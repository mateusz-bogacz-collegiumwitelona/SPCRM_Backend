using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Domain.State;
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
        private readonly IEmailSender _emailSender;

        public OfferServices(AppDbContext context, ILogger<OfferServices> logger, IEmailSender emailSender)
        {
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
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

            var targetDate = command.NewValidUntil.HasValue
                ? DateTime.SpecifyKind(command.NewValidUntil.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(7);

            var stateCheck = offer.CanExtendValidity(targetDate);
            if (!stateCheck.IsSuccess)
            {
                return stateCheck;
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

            var stateCheck = offer.CanTransitionTo(command.NewStatus);
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to change offer status from {CurrentStatus} to {NewStatus} for ID {OfferId} is invalid.", offer.Status, command.NewStatus, command.OfferId);
                if (_context.Entry(offer).Property(o => o.Status).IsModified)
                {
                    await _context.SaveChangesAsync();
                }

                return Result<Guid?>.Failure(
                    message: stateCheck.Message ?? "An error occurred while changing the offer status.",
                    errorCode: stateCheck.ErrorCode ?? ErrorCodes.InvalidOperation,
                    statusCode: stateCheck.StatusCode
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

        public async Task<Result> UpdateOfferProductsAsync(UpdateOfferProductsCommand command)
        {
            var offer = await _context.Offers
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.Id == command.OfferId);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var stateCheck = offer.CanEditProducts();

            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to update products for offer ID {OfferId} in invalid state.", command.OfferId);
                return stateCheck;
            }

            if (!command.Items.Any())
            {
                _logger.LogWarning("Attempt to update offer with ID {OfferId} with no products.", command.OfferId);
                return Result.Failure(
                    message: "Offer must contain at least one product.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var requestedProductIds = command.Items.Select(i => i.ProductId).Distinct().ToList();
            var existingProductsCount = await _context.Products.CountAsync(p => requestedProductIds.Contains(p.Id));

            if (existingProductsCount != requestedProductIds.Count)
            {
                _logger.LogWarning("One or more products specified in the command do not exist for offer ID {OfferId}.", command.OfferId);
                return Result.Failure(
                    message: "One or more products specified in the command do not exist.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.OfferProducts.RemoveRange(offer.Products);

                var newOfferProducts = command.Items.Select(i => new OfferProducts
                {
                    OfferId = offer.Id,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    QuotedPrice = i.QuotedPrice
                }).ToList();

                await _context.OfferProducts.AddRangeAsync(newOfferProducts);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result.Success(
                    message: "Offer products updated successfully.",
                    statusCode: StatusCodes.Status200OK
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error while updating products for offer ID {OfferId}", command.OfferId);
                throw;
            }
        }

        public async Task<Result> ResendOfferEmailAsync(ResendOfferEmailCommand command)
        {
            var offer = await _context.Offers
                .Include(o => o.Currency)
                .Include(o => o.Contact)
                    .ThenInclude(c => c.ContactDetails)
                .Include(o => o.Products)
                    .ThenInclude(p => p.Product)
                        .ThenInclude(pr => pr.Unit)
                .Include(o => o.Products)
                    .ThenInclude(p => p.Product)
                        .ThenInclude(pr => pr.SteelGrade)
                .FirstOrDefaultAsync(o => o.Id == command.OfferId);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var statusCheck = offer.CanResendEmail();

            if (!statusCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to resend email for offer ID {OfferId} in invalid state.", command.OfferId);
                return statusCheck;
            }

            if (!statusCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to resend email for offer ID {OfferId} in invalid state.", command.OfferId);
                return Result.Failure(
                    message: statusCheck.Message ?? "Cannot resend email for this offer.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var clientEmails = offer.Contact.ContactDetails
                .Where(cd => cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)
                .Select(cd => cd.Value)
                .ToList();

            if (!clientEmails.Any())
            {
                return Result.Failure(
                    message: "Contact has no primary email address configured.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var mailingItems = offer.Products.Select(op => new MailingProductItemDomain
            {
                ProductId = op.ProductId,
                CurrencyId = offer.CurrencyId,
                ProductName = op.Product.Name,
                SteelGrade = op.Product.SteelGrade?.Name ?? string.Empty,

                FormattedDimensions = DimensionsFormatter.Format(
                    op.Product.Category,
                    op.Product.Diameter,
                    op.Product.Thickness,
                    op.Product.Width,
                    op.Product.Length
                    ),

                Weight = op.Product.Weight,
                UnitSymbol = op.Product.Unit?.Symbol ?? "szt.",
                Quantity = op.Quantity,
                CurrencyCode = offer.Currency.Code,
                FinalPrice = op.QuotedPrice,
                OriginalPrice = null,
                DiscountPercentage = null,
                IsPromoted = false
            }).ToList();

            var offerDomain = new MailingOfferDomain
            {
                BccEmails = clientEmails,
                Language = command.Language ?? "pl",
                Products = mailingItems
            };

            await _emailSender.SendProductMailingAsync(offerDomain);

            return Result.Success(
                message: "Offer email resent successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeleteOfferAsync(Guid id)
        {
            var offer = await _context.Offers
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", id);
                return Result.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var stateCheck = offer.CanDelete();
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to delete an offer with invalid status {OfferStatus} for ID {OfferId}.", offer.Status, id);
                return stateCheck;
            }

            _context.OfferProducts.RemoveRange(offer.Products);
            _context.Offers.Remove(offer);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Offer deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<OfferAllowedActionsResponse>> GetOfferAllowedActionsAsync(Guid id)
        {
            var offer = await _context.Offers
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                return Result<OfferAllowedActionsResponse>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var allowedTransitions = new List<string>();

            if (offer.CanTransitionTo(OfferStatusEnum.Accepted).IsSuccess)
            {
                allowedTransitions.Add(OfferStatusEnum.Accepted.ToString());
            }

            if (offer.CanTransitionTo(OfferStatusEnum.Rejected).IsSuccess)
            {
                allowedTransitions.Add(OfferStatusEnum.Rejected.ToString());
            }

            var response = new OfferAllowedActionsResponse
            {
                CanEdit = offer.CanEditProducts().IsSuccess,
                CanDelete = offer.CanDelete().IsSuccess,
                CanResendEmail = offer.CanResendEmail().IsSuccess,
                CanExtendValidity = offer.Status != OfferStatusEnum.Accepted && offer.Status != OfferStatusEnum.Rejected,
                AllowedStatusTransitions = allowedTransitions
            };

            return Result<OfferAllowedActionsResponse>.Success(
                message: "Allowed actions retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<List<string>>> GetOfferStatus()
            => Result<List<string>>.Success(
                message: "Allowed actions retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames<OfferStatusEnum>().ToList()
            );
    }
}
