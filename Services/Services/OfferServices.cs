using Domain.Common;
using Domain.Constants;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    }
}
