using Domain.Common;
using Domain.Models;
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
            SortingRequest sorting,
            SearchRequest search
            )
        {
            var query = _context.Contacts
                .ApplyFilter(filter)
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .AsNoTracking()
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
                .ApplySorting(sorting);

            return await query.ToPagedResultAsync(pagged, _logger, "contacts");
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
                .AsNoTracking()
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

            return await query.ToPagedResultAsync(pagged, _logger, "company_contacts");
        }

        public async Task<Result<ContactsResponse>> GetContactDetailAsync(Guid contactId)
        {
            var response = await _context.Contacts
                .Where(c => c.Id == contactId)
                .AsNoTracking()
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
                .FirstOrDefaultAsync();

            return Result<ContactsResponse>.Success(
                data: response,
                message: "Contact details retrieved successfully",
                statusCode: StatusCodes.Status200OK 
                );
        }

        public async Task<Result<List<ContactWayResponse>>> GetContactWayAsync(Guid contactId)
        {
            var query = await _context.ContactDetails
                .Where(c => c.ContactId == contactId)
                .AsNoTracking()
                .Select(c => new ContactWayResponse
                {
                    Type = c.Type.ToString(),
                    Value = c.Value,
                    Label = c.Label ?? "",
                    IsPrimary = c.IsPrimary
                })
                .ToListAsync();

            return Result<List<ContactWayResponse>>.Success(
                message: "Contact detail review successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(Guid contatcId, PaggedRequest pagged, SearchRequest search)
        {
            var query = _context.Notes
                .OfType<ContactNote>()
                .Include(n => n.Author)
                .Where(n => n.ContactId == contatcId && !n.IsDeleted)
                .AsNoTracking()
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new ContactNoteResponse
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdateAt = n.UpdateAt
                });
                
           return await query.ToPagedResultAsync(pagged, _logger, "contact_notes");
        }
    }
}
