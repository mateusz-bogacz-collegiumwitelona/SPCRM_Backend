using Domain.Common;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.Response;

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

        public async Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(ContactListCommand command)
        {
            var query = _context.Contacts
                .Include(c => c.Company)
                .AsNoTracking()
                .Distinct()
                .ApplyFilter(command.ComapnyName, command.IsPrimary)
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplySorting(command.SortBy, command.SortDescending)
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
                });
                

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "contacts");
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

        public async Task<Result<PagedResult<CompanyContactResponse>>> GetCompanyContactsAsync(CompanyCommand command)
        {
            var query = _context.Contacts
                .Where(c => c.CompanyId == command.CompanyId)
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

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_contacts");
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

        public async Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(NoteListCommand command)
        {
            var query = _context.Notes
                .OfType<ContactNote>()
                .Include(n => n.Author)
                .Where(n => n.ContactId == command.searchId && !n.IsDeleted)
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
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

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "contact_notes");
        }
    }
}
