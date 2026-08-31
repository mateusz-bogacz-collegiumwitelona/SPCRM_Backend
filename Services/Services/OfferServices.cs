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
            var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null)
            {
                _logger.LogWarning("Offer with ID {OfferId} not found.", id);
                return Result<OfferDetailResponse>.Failure(
                    message: "Offer not found.",
                    errorCode: ErrorCodes.OfferNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var response = new OfferDetailResponse
            {
                OfferId = offer.Id,
                OfferName = offer.Name,
                Status = offer.Status.ToString(),
                ValidUntil = offer.ValidUntil,
                IsExpired = offer.ValidUntil < DateTime.UtcNow
            };

            return Result<OfferDetailResponse>.Success(
                message: "Offer details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }
    }
}
