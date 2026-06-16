using Domain.Common;
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

        public async Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(
            PaggedRequest pagged,
            ContactFilterRequest filter,
            SearchRequest search
            )
        {
            var query = _context.Contacts
                .ApplyFilter(filter, search.SearchTerm ?? string.Empty)
                .Distinct()
                .Select(c => new ContactsResponse
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    JobTitle = c.JobTitle ?? "",
                    CompanyName = c.Company.Name,
                    OwnerFirstName = c.Owner.FirstName,
                    OwnerLastName = c.Owner.LastName,
                    IsPrimary = c.IsPrimary
                })
                .ApplySorting(search);

            return await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "contacts");
        }

        public async Task<Result<List<string>>> GetCompaniesAsync()
        {
            var companies = await _context.Contacts
                .Select(c => c.Company.Name)
                .Distinct()
                .ToListAsync();

            return Result<List<string>>.Success(
                message: "Companies retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: companies
                );
        }

        public async Task<Result<PagedResult<CompanyContactResponse>>> GetCompanyContactsAsync(Guid comapnyId, PaggedRequest pagged)
        {
            var query = _context.Contacts
                .Where(c => c.CompanyId == comapnyId)
                .Distinct()
                .Select(c => new CompanyContactResponse
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    JobTitle = c.JobTitle ?? "",
                    IsPrimary = c.IsPrimary,
                    OwnerFirstName = c.Owner.FirstName ?? "",
                    OwnerLastName = c.Owner.LastName ?? ""
                });

            return await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "company_contacts");
        }
    }
}
