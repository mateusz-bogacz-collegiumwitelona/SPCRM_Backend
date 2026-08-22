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
                .ApplyFilter(command.ComapnyName, command.IsPrimary, command.OwnerId)
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
                var contactDetail = new ContactDetail
                {
                    Type = ParseWithString(detail.Type),
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

        public Task<Result<List<string>>> GetContactTypeAsync()
        {
            var contactTypes = Enum.GetNames(typeof(ContactDetailTypeEnum)).ToList();

            var result = Result<List<string>>.Success(
                message: "Contact types retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: contactTypes
            );
            return Task.FromResult(result);
        }

        public async Task<Result> EditContactAsync(EditContactCommand command, Guid currentUserId)
        {
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == command.ContactId);

            if (contact == null)
            {
                _logger.LogError("Contact with id {ContactId} not found.", command.ContactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (!CanModifyContact(currentUserId, contact.OwnerId))
            {
                _logger.LogWarning("User with id {userId} cannot edit contact with this id {contactId}", currentUserId, command.ContactId);
                return Result.Failure(
                    message: "You do not have permission to edit this contact",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.UnauthorizedAccess
                );
            }

            if (!string.IsNullOrEmpty(command.FirstName)) contact.FirstName = command.FirstName;
            if (!string.IsNullOrEmpty(command.LastName)) contact.LastName = command.LastName;
            if (!string.IsNullOrEmpty(command.JobTitle)) contact.JobTitle = command.JobTitle;

            contact.UpdateAt = DateTime.UtcNow;

            var incomingDetailsIds = command.Details
                .Where(d => d.ContactDetailId.HasValue && d.ContactDetailId != Guid.Empty)
                .Select(d => d.ContactDetailId!.Value)
                .ToHashSet();

            var detailsToRemoveIds = await _context.ContactDetails
                .Where(d => d.ContactId == contact.Id && !incomingDetailsIds.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync();

            if (detailsToRemoveIds.Any())
            {
                await _context.ContactDetails
                    .Where(d => detailsToRemoveIds.Contains(d.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.IsDeleted, true)
                        .SetProperty(d => d.UpdateAt, DateTime.UtcNow));
            }

            var newDetailsToAdd = new List<ContactDetail>();

            foreach (var detail in command.Details)
            {
                var typeToSet = ParseWithString(detail.Type);

                if (detail.ContactDetailId.HasValue && detail.ContactDetailId.Value != Guid.Empty)
                {
                    await _context.ContactDetails
                        .Where(d => d.Id == detail.ContactDetailId.Value)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(d => d.Label, detail.Label ?? string.Empty)
                            .SetProperty(d => d.Value, detail.Value ?? string.Empty)
                            .SetProperty(d => d.Type, typeToSet)
                            .SetProperty(d => d.IsPrimary, detail.IsPrimary ?? false)
                            .SetProperty(d => d.UpdateAt, DateTime.UtcNow));
                }
                else
                {
                    newDetailsToAdd.Add(new ContactDetail
                    {
                        Type = typeToSet,
                        Value = detail.Value!,
                        Label = detail.Label,
                        IsPrimary = detail.IsPrimary ?? false,
                        ContactId = contact.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            if (newDetailsToAdd.Any())
            {
                await _context.ContactDetails.AddRangeAsync(newDetailsToAdd);
            }

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Contact updated successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<ContactDetailCommand>> GetContactDetailCommand(Guid contactId)
        {
            var contact = await _context.Contacts
                .AsNoTracking()
                .Include(c => c.ContactDetails)
                .Where(c => c.Id == contactId)
                .Select(c => new ContactDetailCommand
                {
                    ContactId = c.Id,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    JobTitle = c.JobTitle ?? string.Empty,
                    Details = c.ContactDetails.Select(cd => new ContactDetailDetailCommand
                    {
                        ContactDetailId = cd.Id,
                        Label = cd.Label ?? string.Empty,
                        Value = cd.Value,
                        IsPrimary = cd.IsPrimary,
                        Type = cd.Type.ToString()
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (contact == null)
            {
                return Result<ContactDetailCommand>.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            return Result<ContactDetailCommand>.Success(
                message: "Contact detail review successfully",
                statusCode: StatusCodes.Status200OK,
                data: contact
            );
        }

        public async Task<Result> SetPrimaryContactAsync(Guid contactId, Guid currentUserId)
        {
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);

            if (contact == null)
            {
                _logger.LogError("Contact with id {ContactId} not found.", contactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (!CanModifyContact(currentUserId, contact.OwnerId))
            {
                _logger.LogWarning("User with id {userId} cannot edit contact with this id {contactId}", currentUserId, contactId);
                return Result.Failure(
                    message: "You do not have permission to edit this contact",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.UnauthorizedAccess
                );
            }

            if (contact.IsPrimary)
            {
                return Result.Failure(
                    message: "This contact is already the primary contact for the company.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.PrimaryContactDetailRequired
                );
            }

            await _context.Contacts
                .Where(c => c.CompanyId == contact.CompanyId && c.IsPrimary && c.Id != contactId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPrimary, false));

            contact.IsPrimary = true;
            contact.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Contact changed to primary successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeleteContactAsync(Guid contactId)
        {
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);

            if (contact == null)
            {
                _logger.LogError("Contact with id {ContactId} not found.", contactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Contact deleted successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeContactOwnerAsync(ChangeContactOwnerCommand command)
        {
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == command.ContactId);

            if (contact == null)
            {
                _logger.LogError("Contact with id {ContactId} not found.", command.ContactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            var isNewOwnerExist = await _context.Users.AnyAsync(u => u.Id == command.NewOwnerId);

            if (!isNewOwnerExist)
            {
                _logger.LogError("User with id {UserId} not found.", command.NewOwnerId);
                return Result.Failure(
                    message: "New owner not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            contact.OwnerId = command.NewOwnerId;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Contact owner changed successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<List<OwnerResponse>>> GetAvailableOwnersAsync()
        {
            var owners = await _context.Users
                .Where(u => !_context.UserRoles.Any(ur =>
                    ur.UserId == u.Id && _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN"))
                )
                .Select(u => new OwnerResponse
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = _context.Roles
                        .Where(r => _context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                        .Select(r => r.Name)
                        .FirstOrDefault() ?? "Brak"
                })
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            return Result<List<OwnerResponse>>.Success(
                message: "Available owners retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: owners
            );
        }

        private ContactDetailTypeEnum ParseWithString(string? name)
            => Enum.TryParse<ContactDetailTypeEnum>(name, ignoreCase: true, out var result)
                ? result
                : ContactDetailTypeEnum.OTHER;

        private bool CanModifyContact(Guid userId, Guid ownerId)
        {
            if (userId == ownerId) return true;

            var isManager = _context.UserRoles
                .Any(ur => ur.UserId == userId &&
                                _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "MANAGER"));

            return isManager;
        }
    }
}
