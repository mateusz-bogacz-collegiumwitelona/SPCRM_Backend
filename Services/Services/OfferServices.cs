using Domain.Common;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Offer;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response;

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
                    command.Status
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
                    Status = o.Status.ToString()
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "offers");
    }
}
