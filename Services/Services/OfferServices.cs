using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
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
            var offerData = await (
                from o in _context.Offers.AsNoTracking()
                where o.Id == id
                join u in _context.Users.AsNoTracking() on o.CreatedByUserId equals u.Id into userGroup
                from u in userGroup.DefaultIfEmpty()
                select new
                {
                    Offer = o,
                    AuthorFirstName = u != null ? u.FirstName : null,
                    AuthorLastName = u != null ? u.LastName : null,
                    AuthorExists = u != null
                }
            ).FirstOrDefaultAsync();

            if (offerData == null)
            {
                _logger.LogInformation("Offer with ID {OfferId} not found.", id);
                return Result<OfferDetailResponse>.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (!offerData.AuthorExists)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has orphaned CreatedByUserId {UserId}.", id, offerData.Offer.CreatedByUserId);
                throw new DataCorruptionException($"Offer '{id}' is linked to non-existent creator '{offerData.Offer.CreatedByUserId}'.");
            }

            var response = new OfferDetailResponse
            {
                OfferId = offerData.Offer.Id,
                OfferName = offerData.Offer.Name,
                Status = offerData.Offer.Status.ToString(),
                ValidUntil = offerData.Offer.ValidUntil,
                IsExpired = offerData.Offer.ValidUntil < DateTime.UtcNow,
                CreatedByUserFirstName = offerData.AuthorFirstName ?? string.Empty,
                CreatedByUserLastName = offerData.AuthorLastName ?? string.Empty
            };

            return Result<OfferDetailResponse>.Success(
                message: "Offer details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<OfferClientDetail>> GetOfferClientDetailAsync(Guid id)
        {
            var offerDetail = await (
                from o in _context.Offers.AsNoTracking()
                where o.Id == id
                join c in _context.Contacts.AsNoTracking() on o.ContactId equals c.Id into contactGroup
                from c in contactGroup.DefaultIfEmpty()
                join comp in _context.Companies.AsNoTracking() on c.CompanyId equals comp.Id into companyGroup
                from comp in companyGroup.DefaultIfEmpty()
                select new
                {
                    OfferExists = true,
                    ContactId = o.ContactId,
                    HasContact = c != null,
                    ContactFirstName = c != null ? c.FirstName : null,
                    ContactLastName = c != null ? c.LastName : null,
                    ContactJobTitle = c != null ? c.JobTitle : null,
                    HasCompany = comp != null,
                    CompanyName = comp != null ? comp.Name : null
                }
            ).FirstOrDefaultAsync();

            if (offerDetail == null)
            {
                _logger.LogInformation("Offer with ID {OfferId} not found.", id);
                return Result<OfferClientDetail>.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (!offerDetail.HasContact || !offerDetail.HasCompany)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has missing contact or company relation.", id);
                throw new DataCorruptionException($"Offer '{id}' has corrupted contact or company linkage.");
            }

            var response = new OfferClientDetail
            {
                ContactId = offerDetail.ContactId,
                ContactFirstName = offerDetail.ContactFirstName!,
                ContactLastName = offerDetail.ContactLastName!,
                ContactJobTitle = offerDetail.ContactJobTitle ?? string.Empty,
                CompanyName = offerDetail.CompanyName!
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
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(o => new
                {
                    o.Id,
                    o.CurrencyId,
                    CurrencyCode = o.Currency != null ? o.Currency.Code : null
                })
                .FirstOrDefaultAsync();

            if (offer == null)
            {
                _logger.LogInformation("Offer with ID {OfferId} not found.", id);
                return Result<PagedResult<OfferProductResponse>>.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.CurrencyId == Guid.Empty || string.IsNullOrWhiteSpace(offer.CurrencyCode))
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has invalid or missing currency.", id);
                throw new DataCorruptionException($"Offer '{id}' has missing currency relation.");
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
                _logger.LogInformation("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.ContactId == Guid.Empty || offer.CreatedByUserId == Guid.Empty || offer.CurrencyId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has missing critical foreign keys.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' has corrupted relational integrity.");
            }

            var targetDate = command.NewValidUntil.HasValue
                ? DateTime.SpecifyKind(command.NewValidUntil.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(7);

            var stateCheck = offer.CanExtendValidity(targetDate);
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Cannot extend validity for offer {OfferId}: {Reason}", offer.Id, stateCheck.Message);
                return stateCheck;
            }

            offer.ValidUntil = targetDate;

            if (offer.Status == OfferStatusEnum.Expired) offer.Status = OfferStatusEnum.Sent;

            offer.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Offer {OfferId} validity extended until {ValidUntil} (Status: {Status}).",
                offer.Id, offer.ValidUntil, offer.Status);

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
                _logger.LogInformation("Offer with ID {OfferId} not found.", command.OfferId);
                return Result<Guid?>.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.Contact == null ||
                offer.Contact.CompanyId == Guid.Empty ||
                offer.CreatedByUserId == Guid.Empty ||
                offer.CurrencyId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has corrupted Contact, Company, User or Currency linkage.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' is missing essential relational data to proceed with status transition.");
            }

            var stateCheck = offer.CanTransitionTo(command.NewStatus);
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Invalid status transition for Offer {OfferId} from {CurrentStatus} to {NewStatus}. Reason: {Reason}",
                    offer.Id, offer.Status, command.NewStatus, stateCheck.Message);

                return Result<Guid?>.Failure(
                    message: stateCheck.Message ?? "Invalid status transition.",
                    statusCode: stateCheck.StatusCode,
                    errorCode: stateCheck.ErrorCode ?? ErrorCodes.InvalidOperation
                );
            }

            if (command.NewStatus == OfferStatusEnum.Accepted)
            {
                if (!offer.Products.Any())
                {
                    _logger.LogWarning("Cannot accept offer {OfferId}: no products associated.", offer.Id);
                    return Result<Guid?>.Failure(
                        message: "Cannot accept an offer without any products.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation
                    );
                }

                var hasCorruptedProducts = offer.Products.Any(p => p.Quantity <= 0 || p.QuotedPrice < 0 || p.ProductId == Guid.Empty);
                if (hasCorruptedProducts)
                {
                    _logger.LogError("Critical data corruption: Offer {OfferId} contains products with invalid quantity, price or missing ProductId.", offer.Id);
                    throw new DataCorruptionException($"Offer '{offer.Id}' contains corrupted product line items.");
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                offer.Status = command.NewStatus;
                offer.UpdateAt = DateTime.UtcNow;
                Guid? createdDealId = null;

                if (command.NewStatus == OfferStatusEnum.Accepted)
                {
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

                _logger.LogInformation("Offer {OfferId} status changed to {NewStatus}. Created Deal ID: {DealId}",
                    offer.Id, command.NewStatus, createdDealId);

                return Result<Guid?>.Success(
                    message: command.NewStatus == OfferStatusEnum.Accepted
                        ? "Offer accepted and converted to sale deal successfully."
                        : "Offer status updated successfully.",
                    statusCode: StatusCodes.Status200OK,
                    data: createdDealId
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction failed while changing offer status for Offer {OfferId} to {NewStatus}", command.OfferId, command.NewStatus);
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
                _logger.LogInformation("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.CurrencyId == Guid.Empty || offer.ContactId == Guid.Empty || offer.CreatedByUserId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has missing critical foreign keys.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' has corrupted relational integrity.");
            }

            var stateCheck = offer.CanEditProducts();
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to update products for offer {OfferId} in invalid state: {Message}", command.OfferId, stateCheck.Message);
                return stateCheck;
            }

            if (command.Items == null || !command.Items.Any())
            {
                _logger.LogWarning("Attempt to update offer {OfferId} with empty product list.", command.OfferId);
                return Result.Failure(
                    message: "Offer must contain at least one product.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var hasInvalidItems = command.Items.Any(i => i.Quantity <= 0 || i.QuotedPrice < 0 || i.ProductId == Guid.Empty);
            if (hasInvalidItems)
            {
                _logger.LogWarning("Attempt to update offer {OfferId} with invalid product line item values (Quantity <= 0 or Price < 0).", command.OfferId);
                return Result.Failure(
                    message: "All offer items must have positive quantity and non-negative quoted price.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var requestedProductIds = command.Items.Select(i => i.ProductId).Distinct().ToList();

            var existingProducts = await _context.Products
                .AsNoTracking()
                .Where(p => requestedProductIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.PricePerUnit,
                    p.CurrencyId
                })
                .ToListAsync();

            var missingProducts = requestedProductIds.Except(existingProducts.Select(p => p.Id)).ToList();
            if (missingProducts.Any())
            {
                _logger.LogInformation("One or more products do not exist for offer {OfferId}: {MissingProducts}",
                    command.OfferId, string.Join(", ", missingProducts));

                return Result.Failure(
                    message: "One or more products specified in the command do not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound,
                    errors: missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
            }

            var corruptedProduct = existingProducts.FirstOrDefault(p => p.PricePerUnit < 0 || p.CurrencyId == Guid.Empty);
            if (corruptedProduct != null)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has negative price or missing currency.", corruptedProduct.Id);
                throw new DataCorruptionException($"Product '{corruptedProduct.Id}' has corrupted pricing or currency state.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.OfferProducts.RemoveRange(offer.Products);

                var newOfferProducts = command.Items.Select(i => new OfferProducts
                {
                    Id = Guid.NewGuid(),
                    OfferId = offer.Id,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    QuotedPrice = i.QuotedPrice
                }).ToList();

                await _context.OfferProducts.AddRangeAsync(newOfferProducts);

                offer.UpdateAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Offer {OfferId} products updated successfully ({Count} items).", offer.Id, newOfferProducts.Count);

                return Result.Success(
                    message: "Offer products updated successfully.",
                    statusCode: StatusCodes.Status200OK
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction failed while updating products for offer ID {OfferId}", command.OfferId);
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
                _logger.LogInformation("Offer with ID {OfferId} not found.", command.OfferId);
                return Result.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.Contact == null || offer.Currency == null || string.IsNullOrWhiteSpace(offer.Currency.Code))
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has missing contact or currency.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' has corrupted contact or currency state.");
            }

            var statusCheck = offer.CanResendEmail();
            if (!statusCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to resend email for offer {OfferId} in invalid state: {Message}", command.OfferId, statusCheck.Message);
                return statusCheck;
            }

            if (offer.Products == null || !offer.Products.Any())
            {
                _logger.LogWarning("Attempt to resend email for offer {OfferId} with no products.", command.OfferId);
                return Result.Failure(
                    message: "Cannot resend an offer without any products.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var clientEmails = offer.Contact.ContactDetails
                .Where(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)
                .Select(cd => cd.Value.Trim())
                .Where(email => !string.IsNullOrEmpty(email))
                .Distinct()
                .ToList();

            if (!clientEmails.Any())
            {
                _logger.LogWarning("Contact {ContactId} associated with offer {OfferId} has no active primary email.", offer.ContactId, offer.Id);
                return Result.Failure(
                    message: "Contact has no primary email address configured.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var corruptedItem = offer.Products.FirstOrDefault(op => op.Product == null || op.Quantity <= 0 || op.QuotedPrice < 0);
            if (corruptedItem != null)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} contains corrupted line items or missing products.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' contains corrupted product items.");
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
                Language = !string.IsNullOrWhiteSpace(command.Language) ? command.Language : "pl",
                Products = mailingItems
            };

            await _emailSender.SendProductMailingAsync(offerDomain);

            _logger.LogInformation("Offer {OfferId} email resent successfully to {Emails}.", offer.Id, string.Join(", ", clientEmails));

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
                _logger.LogInformation("Offer with ID {OfferId} not found.", id);
                return Result.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
                );
            }

            if (offer.CurrencyId == Guid.Empty || offer.ContactId == Guid.Empty || offer.CreatedByUserId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Offer {OfferId} has missing critical foreign keys.", offer.Id);
                throw new DataCorruptionException($"Offer '{offer.Id}' has corrupted relational integrity.");
            }

            var stateCheck = offer.CanDelete();
            if (!stateCheck.IsSuccess)
            {
                _logger.LogWarning("Attempt to delete an offer in invalid state {OfferStatus} for ID {OfferId}: {Reason}",
                    offer.Status, id, stateCheck.Message);
                return stateCheck;
            }

            if (offer.Products.Any())
            {
                _context.OfferProducts.RemoveRange(offer.Products);
            }

            _context.Offers.Remove(offer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Offer {OfferId} and its line items deleted successfully.", id);

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
                _logger.LogInformation("Offer with ID {OfferId} not found.", id);
                return Result<OfferAllowedActionsResponse>.Failure(
                    message: "Offer not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.OfferNotFound
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
                CanExtendValidity = offer.CanExtendValidity(DateTime.UtcNow.AddDays(7)).IsSuccess,
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
                message: "Offer statuses retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames<OfferStatusEnum>().ToList()
            );
    }
}
