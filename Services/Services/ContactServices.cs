using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
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


        public async Task<Result<PagedResult<MailingClientResponse>>> GetClientDataToMailingAsync(SimpleListCommand command)
        {
            var query = _context.Contacts
                .Include(c => c.Company)
                .AsNoTracking()
                .Distinct()
                .Where(c => c.IsPrimary)
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .Select(c => new MailingClientResponse
                {
                    CompanyName = c.Company.Name,
                    Nip = c.Company.NIP,
                    ContactFirstName = c.FirstName,
                    ContactLastName = c.LastName,
                    ContactId = c.Id
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "mailing_clients");
        }

        public async Task<Result> AddContactAsync(AddContactCommand command, Guid userId)
        {
            var company = await _context.Companies.AnyAsync(c => c.Id == command.CompanyId);

            if (!company)
            {
                _logger.LogWarning("Company with ID {CompanyId} not found.", command.CompanyId);
                return Result.Failure(
                    message: "Company not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                    );
            }

            var hasAnyPrimaryContact = await _context.Contacts
                .AnyAsync(c => c.CompanyId == command.CompanyId && c.IsPrimary);

            var owner = await _context.Users.FindAsync(userId);
            if (owner == null)
            {
                return Result.Failure(
                   message: "User not found",
                   statusCode: StatusCodes.Status404NotFound,
                   errorCode: ErrorCodes.UserNotFound
                );
            }

            var contact = new Contact
            {
                CompanyId = command.CompanyId,
                FirstName = command.FirstName,
                LastName = command.LastName,
                JobTitle = command.JobTitle,
                OwnerId = userId,
                Owner = owner,
                IsPrimary = !hasAnyPrimaryContact
            };

            foreach (var detail in command.Details)
            {
                var parsedType = Enum.Parse<ContactDetailTypeEnum>(detail.Type, ignoreCase: true);

                var contactDetail = new ContactDetail
                {
                    Type = parsedType,
                    Value = detail.Value,
                    Label = detail.Label,
                    IsPrimary = detail.IsPrimary,
                    Contact = contact
                };

                contact.ContactDetails.Add(contactDetail);
            }

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Contact added successfully",
                statusCode: StatusCodes.Status201Created
                );
        }

    }
}
