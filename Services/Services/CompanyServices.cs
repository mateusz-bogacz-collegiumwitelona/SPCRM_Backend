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
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;

namespace Services.Services
{
    public class CompanyServices : ICompanyServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CompanyServices> _logger;
        private readonly IEntityAuthorizationService _entityAuth;
        public CompanyServices(
            AppDbContext context,
            ILogger<CompanyServices> logger,
            IEntityAuthorizationService entityAuth)
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
        }

        public async Task<Result<List<CompaniesMapResponse>>> Map(string? searchTerm = null)
        {
            var query = _context.CompanyAdresses.AsQueryable();

            var hasInvalidCoordinates = await query.AnyAsync(a => a.Location == null);
            if (hasInvalidCoordinates)
            {
                throw new MissingCoordinatesException("Data integrity violation: Found addresses without geographic coordinates.");
            }


            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();

                query = query.Where(a =>
                    a.Company.Name.ToLower().Contains(term) ||
                    a.Company.NIP.Contains(term) ||
                    a.City.ToLower().Contains(term) ||
                    a.ZipCode.Contains(term)
                );
            }

            var response = await query
                .Select(a => new CompaniesMapResponse
                {
                    Id = a.Company.Id,
                    Name = a.Company.Name,
                    Nip = a.Company.NIP,
                    City = a.City,
                    Street = a.Street,
                    ZipCode = a.ZipCode,
                    Latitude = a.Location != null ? a.Location.Y : (double?)null,
                    Longitude = a.Location != null ? a.Location.X : (double?)null,
                    Type = a.AddressType.ToString()
                })
                .ToListAsync();

            return Result<List<CompaniesMapResponse>>.Success(
                message: "Company list retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<CompanyDetailResponse>> Details(Guid id, Guid userId)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == id);

            if (company == null)
            {
                _logger.LogInformation("User with id: {userId} want see comapny with id {companyID} who doesn't exist.", userId, id);
                return Result<CompanyDetailResponse>.Failure(
                    message: "Company not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                    );
            }

            var response = new CompanyDetailResponse
            {
                Id = company.Id,
                Name = company.Name,
                Nip = company.NIP,
                IsYour = company.OwnerId == userId
            };

            return Result<CompanyDetailResponse>.Success(
                message: "Company details fetched successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<AddressDetailResponse>>> GetCompanyAddresses(CompanyCommand command)
            => await _context.CompanyAdresses
                .Where(a => a.CompanyId == command.CompanyId)
                .Select(a => new AddressDetailResponse
                {
                    Id = a.Id,
                    Street = a.Street,
                    City = a.City,
                    ZipCode = a.ZipCode,
                    Latitude = a.Location != null ? a.Location.Y : (double?)null,
                    Longitude = a.Location != null ? a.Location.X : (double?)null,
                    Type = a.AddressType.ToString()
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "comapny_adresses");


        public async Task<Result<PagedResult<CompanyResponse>>> GetCompanyListAsync(CompanyListCommand command)
            => await _context.Companies
                    .ApplyFiler(command.IsYour, command.CreatedAtFrom, command.CreatedAtTo, command.UserId)
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .Where(c => c.CompanyAdresses.Any(ca => ca.AddressType == AddressTypeEnum.Headquarters))
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(c => new CompanyResponse
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Nip = c.NIP,

                        LastDealDate = c.Deals
                            .OrderByDescending(d => d.CreatedAt)
                            .Select(d => (DateTime?)d.CreatedAt)
                            .FirstOrDefault(),

                        IsYour = c.OwnerId == command.UserId,
                        OwnerFirstName = c.OwnerId == command.UserId ? null : c.Owner.FirstName,
                        OwnerLastName = c.OwnerId == command.UserId ? null : c.Owner.LastName,

                        City = c.CompanyAdresses
                            .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                            .Select(ca => ca.City)
                            .FirstOrDefault() ?? string.Empty,

                        Street = c.CompanyAdresses
                            .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                            .Select(ca => ca.Street)
                            .FirstOrDefault() ?? string.Empty,

                        ZipCode = c.CompanyAdresses
                            .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                            .Select(ca => ca.ZipCode)
                            .FirstOrDefault() ?? string.Empty,

                        CreatedAt = c.CreatedAt,
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "companies");

        public async Task<Result<List<CompanySimpleListResponse>>> GetCompanySimpleListAsync()
        {
            var query = await _context.Companies
                .OrderBy(c => c.Name)
                .Select(c => new CompanySimpleListResponse
                {
                    Id = c.Id,
                    Name = c.Name
                }).ToListAsync();

            return Result<List<CompanySimpleListResponse>>.Success(
                message: "Company simple list retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<Guid>> AddCompanyAsync(AddCompanyCommand command, Guid userId)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId && !u.IsDeleted);

            if (!userExists)
            {
                _logger.LogError("Critical data inconsistency: Attempt to create company by non-existent user {UserId}", userId);
                throw new UserNotFoundException(userId);
            }

            var companyExists = await _context.Companies
                 .AsNoTracking()
                 .AnyAsync(c => EF.Functions.ILike(c.Name, command.Name) || c.NIP == command.NIP);

            if (companyExists)
            {
                _logger.LogWarning(
                    "User {UserId} tried to add company with existing Name '{CompanyName}' or NIP '{NIP}'.",
                    userId, command.Name, command.NIP
                );

                return Result<Guid>.Failure(
                    message: "Company with the same name or NIP already exists.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.CompanyAlreadyExists
                );
            }

            var addresses = command.Adresses ?? new List<AddCompanyAdressCommand>();

            if (!addresses.Any())
            {
                _logger.LogWarning(
                    "User {UserId} tried to add company '{CompanyName}' without any addresses.",
                    userId, command.Name
                );
                return Result<Guid>.Failure(
                    message: "Company must have at least one address.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            if (addresses.Count(a => a.Type == AddressTypeEnum.Headquarters) != 1)
            {
                _logger.LogWarning(
                    "User {UserId} tried to add company '{CompanyName}' with {HeadquartersCount} headquarters addresses.",
                    userId, command.Name, addresses.Count(a => a.Type == AddressTypeEnum.Headquarters)
                );
                return Result<Guid>.Failure(
                    message: "Company must have exactly one headquarters address.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var hasDuplicateAddresses = addresses
                .GroupBy(a => new { a.Type, Street = a.Street.Trim().ToLower(), City = a.City.Trim().ToLower(), ZipCode = a.ZipCode.Trim() })
                .Any(g => g.Count() > 1);

            if (hasDuplicateAddresses)
            {
                _logger.LogWarning(
                    "User {UserId} tried to add company '{CompanyName}' with duplicate addresses.",
                    userId, command.Name
                );
                return Result<Guid>.Failure(
                    message: "Cannot add duplicate addresses for the same company.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.AddressAlreadyExists
                );
            }

            var companyId = Guid.NewGuid();

            var company = new Company
            {
                Id = companyId,
                Name = command.Name,
                NIP = command.NIP,
                OwnerId = userId,
                CompanyAdresses = addresses
                    .Select(addr => CreateAddressEntity(addr, companyId))
                    .ToList()
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyName} (ID: {CompanyId}) created by user {UserId}.", company.Name, company.Id, userId);

            return Result<Guid>.Success(
                message: "Company added successfully.",
                statusCode: StatusCodes.Status201Created,
                data: company.Id
            );
        }

        public async Task<Result> EditCompanyAsync(EditCompanyCommand command, Guid userId)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == command.Id);

            if (company == null)
            {
                _logger.LogInformation("Company with id: {CompanyId} doesn't exist.", command.Id);
                return Result.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            if (company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Company {CompanyId} has empty OwnerId.", company.Id);
                throw new DataCorruptionException($"Company '{company.Id}' has no assigned owner.");
            }

            if (!await _entityAuth.CanModifyAsync(userId, company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to modify company {CompanyId} without permission.", userId, command.Id);
                throw new ForbiddenException("You are not authorized to modify this company.");
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                var trimmedName = command.Name.Trim();
                var nameExists = await _context.Companies
                    .AnyAsync(c => c.Id != command.Id && EF.Functions.ILike(c.Name, trimmedName));

                if (nameExists)
                {
                    _logger.LogInformation("Company with name: {CompanyName} already exists.", trimmedName);
                    return Result.Failure(
                        message: "Company with the same name already exists.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.CompanyAlreadyExists
                    );
                }

                company.Name = trimmedName;
            }

            if (!string.IsNullOrWhiteSpace(command.NIP))
            {
                var cleanNip = command.NIP.Replace("-", "").Replace(" ", "").Trim();
                var nipExists = await _context.Companies
                    .AnyAsync(c => c.Id != command.Id && c.NIP == cleanNip);

                if (nipExists)
                {
                    _logger.LogInformation("Company with NIP: {CompanyNIP} already exists.", cleanNip);
                    return Result.Failure(
                        message: "Company with the same NIP already exists.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.CompanyAlreadyExists
                    );
                }

                company.NIP = cleanNip;
            }

            _logger.LogInformation("Company {CompanyName} (ID: {CompanyId}) updated by user {UserId}.", company.Name, company.Id, userId);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Company updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditCompanyAddressAsync(EditCompanyAddressCommand command, Guid userId)
        {
            var address = await _context.CompanyAdresses
                .Include(ca => ca.Company)
                .FirstOrDefaultAsync(ca => ca.Id == command.AddressId);

            if (address == null)
            {
                _logger.LogInformation("Address with id: {AddressId} doesn't exist.", command.AddressId);
                return Result.Failure(
                    message: "Address not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.AddressNotFound
                );
            }

            if (address.Company == null || address.Company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Address {AddressId} is missing valid company navigation or OwnerId.", address.Id);
                throw new DataCorruptionException($"Address '{address.Id}' is linked to an invalid or orphaned company.");
            }

            if (!await _entityAuth.CanModifyAsync(userId, address.Company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to modify address {AddressId} without permission.", userId, command.AddressId);
                throw new ForbiddenException("You are not authorized to modify this address.");
            }

            if (!string.IsNullOrWhiteSpace(command.Street))
            {
                address.Street = command.Street.Trim();
            }

            if (!string.IsNullOrWhiteSpace(command.City))
            {
                address.City = command.City.Trim();
            }

            if (!string.IsNullOrWhiteSpace(command.ZipCode))
            {
                address.ZipCode = command.ZipCode.Trim();
            }

            if (command.Location != null)
            {
                address.Location = command.Location;
            }

            if (command.Type.HasValue)
            {
                var newType = command.Type.Value;

                if (newType == AddressTypeEnum.Headquarters && address.AddressType != AddressTypeEnum.Headquarters)
                {
                    var currentHq = await _context.CompanyAdresses
                        .FirstOrDefaultAsync(ca => ca.CompanyId == address.CompanyId &&
                                                   ca.AddressType == AddressTypeEnum.Headquarters &&
                                                   ca.Id != address.Id);

                    if (currentHq != null)
                    {
                        currentHq.AddressType = AddressTypeEnum.Branch;
                        _logger.LogInformation(
                            "Demoted previous headquarters {OldHqId} to Branch for company {CompanyId}.",
                            currentHq.Id, address.CompanyId);
                    }

                    address.AddressType = AddressTypeEnum.Headquarters;
                }
                else if (address.AddressType == AddressTypeEnum.Headquarters && newType != AddressTypeEnum.Headquarters)
                {
                    var hasOtherHq = await _context.CompanyAdresses
                        .AnyAsync(ca => ca.CompanyId == address.CompanyId &&
                                        ca.AddressType == AddressTypeEnum.Headquarters &&
                                        ca.Id != address.Id);

                    if (!hasOtherHq)
                    {
                        _logger.LogWarning("Attempted to demote the only headquarters for company {CompanyId}.", address.CompanyId);
                        return Result.Failure(
                            message: "The company must have a headquarters. To change this address, " +
                            "first designate a different address as the headquarters or " +
                            "edit the details of the current headquarters.",
                            statusCode: StatusCodes.Status400BadRequest,
                            errorCode: ErrorCodes.InvalidOperation
                        );
                    }

                    address.AddressType = newType;
                }
                else
                {
                    address.AddressType = newType;
                }
            }

            _logger.LogInformation("Address {AddressId} updated by user {UserId}.", address.Id, userId);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Address updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<Guid>> AddCompanyAddressAsync(AddCompanyAdressCommand command, Guid userId, Guid companyId)
        {
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                _logger.LogInformation("Company with id: {CompanyId} doesn't exist.", companyId);
                return Result<Guid>.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            if (company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Company {CompanyId} has empty OwnerId.", company.Id);
                throw new DataCorruptionException($"Company '{company.Id}' has no assigned owner.");
            }

            if (!await _entityAuth.CanModifyAsync(userId, company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to add address to company {CompanyId} without permission.", userId, companyId);
                throw new ForbiddenException("You are not authorized to modify this company.");
            }

            var street = command.Street.Trim();
            var city = command.City.Trim();
            var zipCode = command.ZipCode.Trim();

            var isDuplicate = await _context.CompanyAdresses.AnyAsync(ca =>
                ca.CompanyId == companyId &&
                ca.AddressType == command.Type &&
                EF.Functions.ILike(ca.Street, street) &&
                EF.Functions.ILike(ca.City, city) &&
                ca.ZipCode == zipCode);

            if (isDuplicate)
            {
                _logger.LogWarning("Address already exists for company {CompanyId}.", companyId);
                return Result<Guid>.Failure(
                    message: "This address already exists for this company.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.AddressAlreadyExists
                );
            }

            if (command.Type == AddressTypeEnum.Headquarters)
            {
                var existingHq = await _context.CompanyAdresses
                    .FirstOrDefaultAsync(ca => ca.CompanyId == companyId && ca.AddressType == AddressTypeEnum.Headquarters);

                if (existingHq != null)
                {
                    existingHq.AddressType = AddressTypeEnum.Branch;
                    _logger.LogInformation(
                        "Demoted previous headquarters {OldHqId} to Branch for company {CompanyId}.",
                        existingHq.Id, companyId);
                }
            }

            var newAddress = CreateAddressEntity(command, companyId);

            _context.CompanyAdresses.Add(newAddress);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added address {AddressId} to company {CompanyId} by user {UserId}.", newAddress.Id, companyId, userId);

            return Result<Guid>.Success(
                message: "Address added successfully.",
                statusCode: StatusCodes.Status201Created,
                data: newAddress.Id
            );
        }

        public async Task<Result> DeleteCompanyAsync(Guid companyId, Guid userId)
        {
            var company = await _context.Companies
                .Include(c => c.CompanyAdresses)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                _logger.LogInformation("Company with id: {CompanyId} doesn't exist.", companyId);
                return Result.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            if (company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Company {CompanyId} has empty OwnerId.", company.Id);
                throw new DataCorruptionException($"Company '{company.Id}' has no assigned owner.");
            }

            if (!await _entityAuth.CanModifyAsync(userId, company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to delete company {CompanyId} without permission.", userId, companyId);
                throw new ForbiddenException("You are not authorized to delete this company.");
            }

            var hasFinancialHistory = await _context.Invoices.AnyAsync(i => i.CompanyId == companyId)
                                   || await _context.Deals.AnyAsync(d => d.CompanyId == companyId);

            if (hasFinancialHistory)
            {
                _logger.LogWarning("Attempted to delete company {CompanyId} with existing financial or sales history.", companyId);
                return Result.Failure(
                    message: "A company with a history of transactions or invoices cannot be deleted due to data consistency.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyName} (ID: {CompanyId}) deleted by user {UserId}.", company.Name, company.Id, userId);

            return Result.Success(
                message: "Company deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeleteCompanyAddressAsync(Guid addressId, Guid userId)
        {
            var address = await _context.CompanyAdresses
                .Include(ca => ca.Company)
                .FirstOrDefaultAsync(ca => ca.Id == addressId);

            if (address == null)
            {
                _logger.LogWarning("Address with id: {AddressId} doesn't exist.", addressId);
                return Result.Failure(
                    message: "Address not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.AddressNotFound
                );
            }

            if (address.Company == null || address.Company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Address {AddressId} has missing company or empty OwnerId.", address.Id);
                throw new DataCorruptionException($"Address '{address.Id}' is linked to an invalid or orphaned company.");
            }

            if (!await _entityAuth.CanModifyAsync(userId, address.Company.OwnerId))
            {
                _logger.LogWarning("Security violation: User {UserId} tried to delete address {AddressId} without permission.", userId, addressId);
                throw new ForbiddenException("You are not authorized to delete this address.");
            }

            if (address.AddressType == AddressTypeEnum.Headquarters)
            {
                _logger.LogWarning("Attempted to delete the headquarters address {AddressId} for company {CompanyId}.", addressId, address.CompanyId);
                return Result.Failure(
                    message: "Cannot delete the headquarters address. Please designate another address as headquarters before deleting this one.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var addressCount = await _context.CompanyAdresses
                .CountAsync(ca => ca.CompanyId == address.CompanyId);

            if (addressCount <= 1)
            {
                _logger.LogWarning("Attempted to delete the last remaining address {AddressId} for company {CompanyId}.", addressId, address.CompanyId);
                return Result.Failure(
                    message: "The company must have at least one address.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            _context.CompanyAdresses.Remove(address);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Address {AddressId} deleted by user {UserId}.", addressId, userId);

            return Result.Success(
                message: "Address deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeCompanyOwnerAsync(ChangeCompanyOwnerCommand command)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == command.CompanyId);

            if (company == null)
            {
                _logger.LogInformation("Company with id: {CompanyId} doesn't exist.", command.CompanyId);
                return Result.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            if (company.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Company {CompanyId} has empty OwnerId.", company.Id);
                throw new DataCorruptionException($"Company '{company.Id}' has no assigned owner.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == command.UserId && !u.IsDeleted);

            if (user == null)
            {
                _logger.LogInformation("User with id: {UserId} doesn't exist.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (company.OwnerId == command.UserId)
            {
                return Result.Failure(
                    message: "This user is already the owner of this company.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var userRoleNames = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.NormalizedName)
                                      .ToListAsync();

            if (!userRoleNames.Any())
            {
                _logger.LogError("Critical data inconsistency: User {UserId} has no assigned role.", user.Id);
                throw new MissingUserRoleException(user.Id);
            }

            if (userRoleNames.Contains("ADMIN"))
            {
                _logger.LogWarning("Attempted to assign company {CompanyId} ownership to an admin user {UserId}.", command.CompanyId, command.UserId);
                return Result.Failure(
                    message: "Cannot assign company ownership to an admin user.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            company.OwnerId = command.UserId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyId} ownership changed to user {UserId}.", command.CompanyId, command.UserId);

            return Result.Success(
                message: "Company ownership changed successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result<List<string>> GetCompanyAddressTypes()
            => Result<List<string>>.Success(
                message: "Address types retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames<AddressTypeEnum>().ToList()
            );

        public async Task<Result<EditCompanyDetailResponse>> GetEditCompanyDetailAsync(Guid id)
        {
            var companyData = await _context.Companies
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.NIP,
                    c.OwnerId
                })
                .FirstOrDefaultAsync();

            if (companyData == null)
            {
                _logger.LogInformation("Company with id: {CompanyId} doesn't exist.", id);
                return Result<EditCompanyDetailResponse>.Failure(
                    message: "Company not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CompanyNotFound
                );
            }

            if (companyData.OwnerId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Company {CompanyId} has empty OwnerId.", companyData.Id);
                throw new DataCorruptionException($"Company '{companyData.Id}' has no assigned owner.");
            }

            var response = new EditCompanyDetailResponse
            {
                Id = companyData.Id,
                Name = companyData.Name,
                NIP = companyData.NIP
            };

            return Result<EditCompanyDetailResponse>.Success(
                message: "Company detail retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        private static CompanyAdress CreateAddressEntity(AddCompanyAdressCommand command, Guid? companyId = null)
        {
            return new CompanyAdress
            {
                CompanyId = companyId ?? Guid.Empty,
                Street = command.Street.Trim(),
                City = command.City.Trim(),
                ZipCode = command.ZipCode.Trim(),
                Location = command.Location,
                AddressType = command.Type
            };
        }
    }
}
