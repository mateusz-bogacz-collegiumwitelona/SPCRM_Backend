using Domain.Common;
using Domain.Constants;
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
    public class ContactServices : IContactServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ContactServices> _logger;

        public ContactServices(AppDbContext context, ILogger<ContactServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<ContactsResponse>>>  GetContacts(
            PaggedRequest pagged, 
            ContactFilterRequest filter, 
            SearchRequest search
            )
        {
            try
            {
                var query = _context.Contacts
                    .ApplyFilter(filter,search.SearchTerm ?? string.Empty)
                    .Select(c => new ContactsResponse
                    {
                        Id = c.Id,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        ComapnyName = c.Company.Name,
                        OwnerFirstName = c.Owner.FirstName,
                        OwnerLastName = c.Owner.LastName,
                        IsPrimary = c.ContactDetails.Any(cd => cd.IsPrimary)
                    })
                    .ApplySorting(search);

                return await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "contacts");
            }
            catch (Exception ex)
            {
                return Result<PagedResult<ContactsResponse>>.Failure(
                     message: "An error occurred while retrieving contact details",
                     statusCode: StatusCodes.Status500InternalServerError,
                     errorCode: ErrorCodes.InternalError,
                     errors: new List<string> { ex.Message }
                     );
            }
        }
    }
}
