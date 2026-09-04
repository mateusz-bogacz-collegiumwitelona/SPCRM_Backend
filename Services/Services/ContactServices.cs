using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Company;
using Services.Command.Contact;
using Services.Command.List;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;
using Services.Response.Contact;

namespace Services.Services
{
    public class ContactServices : IContactServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ContactServices> _logger;
        private readonly IEntityAuthorizationService _entityAuth;

        public ContactServices(
            AppDbContext context,
            ILogger<ContactServices> logger,
            IEntityAuthorizationService entityAuth)
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
        }

        public async Task<Result<PagedResult<ContactsResponse>>> GetContactsAsync(ContactListCommand command)
            => await _context.Contacts
                    .Include(c => c.Company)
                    .AsNoTracking()
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
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "contacts");


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
            => await _context.Contacts
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
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_contacts");

        public async Task<Result<ContactsResponse>> GetContactDetailAsync(Guid contactId)
        {
            var contact = await _context.Contacts
                .AsNoTracking()
                .Where(c => c.Id == contactId)
                .Select(c => new
                {
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    JobTitle = c.JobTitle ?? string.Empty,
                    CompanyName = c.Company.Name,
                    c.CompanyId,
                    OwnerFirstName = c.Owner.FirstName,
                    OwnerLastName = c.Owner.LastName,
                    c.OwnerId,
                    c.IsPrimary
                })
                .FirstOrDefaultAsync();

            if (contact == null)
            {
                _logger.LogInformation("Contact with id: {ContactId} doesn't exist.", contactId);
                return Result<ContactsResponse>.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (contact.CompanyId == Guid.Empty || contact.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} is missing CompanyId or OwnerId.", contact.Id);
                throw new DataCorruptionException($"Contact '{contact.Id}' has corrupted company or owner relation.");
            }

            var response = new ContactsResponse
            {
                Id = contact.Id,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                JobTitle = contact.JobTitle,
                CompanyName = contact.CompanyName,
                OwnerFirstName = contact.OwnerFirstName,
                OwnerLastName = contact.OwnerLastName,
                IsPrimary = contact.IsPrimary
            };

            return Result<ContactsResponse>.Success(
                message: "Contact details retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<List<ContactWayResponse>>> GetContactWayAsync(Guid contactId)
        {
            var contactExists = await _context.Contacts
                .AsNoTracking()
                .AnyAsync(c => c.Id == contactId);

            if (!contactExists)
            {
                _logger.LogInformation("Contact with id: {ContactId} doesn't exist.", contactId);
                return Result<List<ContactWayResponse>>.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            var details = await _context.ContactDetails
                .AsNoTracking()
                .Where(c => c.ContactId == contactId)
                .Select(c => new ContactWayResponse
                {
                    Type = c.Type.ToString(),
                    Value = c.Value,
                    Label = c.Label ?? string.Empty,
                    IsPrimary = c.IsPrimary
                })
                .ToListAsync();

            return Result<List<ContactWayResponse>>.Success(
                message: "Contact details retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: details
            );
        }

        public async Task<Result<PagedResult<MailingClientResponse>>> GetClientDataToMailingAsync(SimpleListCommand command)
            => await _context.Contacts
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
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "mailing_clients");

        public async Task<Result> AddContactAsync(AddContactCommand command, Guid userId)
        {
            var company = await _context.Companies
                .AsNoTracking()
                .Select(c => new { c.Id, c.OwnerId })
                .FirstOrDefaultAsync(c => c.Id == command.CompanyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID {CompanyId} not found.", command.CompanyId);
                return Result.Failure(
                    message: "Company not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (owner == null)
            {
                _logger.LogError("Critical data inconsistency: Attempt to add contact by non-existent user {UserId}", userId);
                throw new UserNotFoundException(userId);
            }

            if (!await _entityAuth.CanModifyAsync(userId, company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to add contact to company {CompanyId} without permission.", userId, command.CompanyId);
                throw new ForbiddenException("You do not have permission to add contacts to this company.");
            }

            var hasAnyPrimaryContact = await _context.Contacts
                .AnyAsync(c => c.CompanyId == command.CompanyId && c.IsPrimary);

            var contact = new Contact
            {
                CompanyId = command.CompanyId,
                FirstName = command.FirstName.Trim(),
                LastName = command.LastName.Trim(),
                JobTitle = command.JobTitle?.Trim(),
                OwnerId = userId,
                Owner = owner,
                IsPrimary = !hasAnyPrimaryContact
            };

            if (command.Details != null)
            {
                foreach (var detail in command.Details)
                {
                    contact.ContactDetails.Add(new ContactDetail
                    {
                        Type = ParseWithString(detail.Type),
                        Value = detail.Value.Trim(),
                        Label = detail.Label?.Trim(),
                        IsPrimary = detail.IsPrimary,
                        Contact = contact
                    });
                }
            }

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact {ContactId} added to company {CompanyId} by user {UserId}.", contact.Id, command.CompanyId, userId);

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

            if (contact.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} has empty OwnerId.", contact.Id);
                throw new DataCorruptionException($"Contact '{contact.Id}' has no assigned owner.");
            }

            if (!await _entityAuth.CanModifyAsync(currentUserId, contact.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} cannot edit contact {ContactId}.", currentUserId, command.ContactId);
                throw new ForbiddenException("You do not have permission to edit this contact.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
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
                await transaction.CommitAsync();

                _logger.LogInformation("Contact {ContactId} updated successfully by user {UserId}.", command.ContactId, currentUserId);

                return Result.Success(
                    message: "Contact updated successfully",
                    statusCode: StatusCodes.Status200OK
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to edit contact with ID: {ContactId}", command.ContactId);
                throw;
            }
        }

        public async Task<Result<ContactDetailCommand>> GetContactDetailCommand(Guid contactId)
        {
            var contactData = await _context.Contacts
                .AsNoTracking()
                .Where(c => c.Id == contactId)
                .Select(c => new
                {
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.JobTitle,
                    c.CompanyId,
                    c.OwnerId,
                    Details = c.ContactDetails
                        .Where(cd => !cd.IsDeleted)
                        .Select(cd => new ContactDetailDetailCommand
                        {
                            ContactDetailId = cd.Id,
                            Label = cd.Label ?? string.Empty,
                            Value = cd.Value,
                            IsPrimary = cd.IsPrimary,
                            Type = cd.Type.ToString()
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (contactData == null)
            {
                _logger.LogInformation("Contact with id: {ContactId} doesn't exist.", contactId);
                return Result<ContactDetailCommand>.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (contactData.CompanyId == Guid.Empty || contactData.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} is missing CompanyId or OwnerId.", contactData.Id);
                throw new DataCorruptionException($"Contact '{contactData.Id}' has corrupted company or owner relation.");
            }

            var response = new ContactDetailCommand
            {
                ContactId = contactData.Id,
                FirstName = contactData.FirstName,
                LastName = contactData.LastName,
                JobTitle = contactData.JobTitle ?? string.Empty,
                Details = contactData.Details
            };

            return Result<ContactDetailCommand>.Success(
                message: "Contact detail review successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result> SetPrimaryContactAsync(Guid contactId, Guid currentUserId)
        {
            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);

            if (contact == null)
            {
                _logger.LogInformation("Contact with id {ContactId} not found.", contactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (contact.CompanyId == Guid.Empty || contact.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} is missing CompanyId or OwnerId.", contact.Id);
                throw new DataCorruptionException($"Contact '{contact.Id}' has corrupted company or owner relation.");
            }

            if (!await _entityAuth.CanModifyAsync(currentUserId, contact.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} cannot set primary contact for contact {ContactId}.", currentUserId, contactId);
                throw new ForbiddenException("You do not have permission to edit this contact.");
            }

            if (contact.IsPrimary)
            {
                return Result.Failure(
                    message: "This contact is already the primary contact for the company.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            await _context.Contacts
                .Where(c => c.CompanyId == contact.CompanyId && c.IsPrimary && c.Id != contactId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.IsPrimary, false)
                    .SetProperty(c => c.UpdateAt, DateTime.UtcNow));

            contact.IsPrimary = true;
            contact.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact {ContactId} promoted to primary for company {CompanyId} by user {UserId}.", contact.Id, contact.CompanyId, currentUserId);

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

            if (contact.CompanyId == Guid.Empty || contact.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} has missing CompanyId or OwnerId.", contact.Id);
                throw new DataCorruptionException($"Contact '{contact.Id}' has corrupted company or owner relation.");
            }

            if (contact.IsPrimary)
            {
                var hasOtherContacts = await _context.Contacts
                    .AnyAsync(c => c.CompanyId == contact.CompanyId && c.Id != contactId);

                if (hasOtherContacts)
                {
                    _logger.LogWarning("Attempted to delete primary contact {ContactId} for company {CompanyId} while other contacts exist.", contactId, contact.CompanyId);
                    return Result.Failure(
                        message: "Cannot delete the primary contact. Please assign another contact as primary before deleting this one.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation
                    );
                }
            }

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact {ContactId} deleted successfully.", contactId);

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
                _logger.LogInformation("Contact with id {ContactId} not found.", command.ContactId);
                return Result.Failure(
                    message: "Contact not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ContactNotFound
                );
            }

            if (contact.OwnerId == Guid.Empty || contact.CompanyId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} has empty OwnerId or CompanyId.", contact.Id);
                throw new DataCorruptionException($"Contact '{contact.Id}' has corrupted relations.");
            }

            var newOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == command.NewOwnerId && !u.IsDeleted);

            if (newOwner == null)
            {
                _logger.LogInformation("User with id {UserId} not found.", command.NewOwnerId);
                return Result.Failure(
                    message: "New owner not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (contact.OwnerId == command.NewOwnerId)
            {
                return Result.Failure(
                    message: "This user is already the owner of this contact.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var newOwnerRoleNames = await (from ur in _context.UserRoles
                                           join r in _context.Roles on ur.RoleId equals r.Id
                                           where ur.UserId == newOwner.Id
                                           select r.NormalizedName)
                                          .ToListAsync();

            if (!newOwnerRoleNames.Any())
            {
                _logger.LogError("Critical data inconsistency: User {UserId} has no assigned role.", newOwner.Id);
                throw new MissingUserRoleException(newOwner.Id);
            }

            if (newOwnerRoleNames.Contains("ADMIN"))
            {
                _logger.LogWarning("Attempted to assign contact {ContactId} ownership to an admin user {UserId}.", command.ContactId, command.NewOwnerId);
                return Result.Failure(
                    message: "Cannot assign contact ownership to an admin user.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            contact.OwnerId = command.NewOwnerId;
            contact.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Contact {ContactId} ownership changed to user {UserId}.", command.ContactId, command.NewOwnerId);

            return Result.Success(
                message: "Contact owner changed successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        private ContactDetailTypeEnum ParseWithString(string? name)
            => Enum.TryParse<ContactDetailTypeEnum>(name, ignoreCase: true, out var result)
                ? result
                : ContactDetailTypeEnum.OTHER;

    }
}
