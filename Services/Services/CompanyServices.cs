using Domain.Common;
using Domain.Constants;
using Domain.Enum;
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
        {
            var query = _context.CompanyAdresses
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
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "comapny_adresses");
        }


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
            var companyExists = await _context.Companies
                 .AsNoTracking()
                 .AnyAsync(c => c.Name.ToLower() == command.Name.ToLower() || c.NIP == command.NIP);

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

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = command.Name,
                NIP = command.NIP,
                OwnerId = userId,
                CompanyAdresses = addresses.Select(addr => new CompanyAdress
                {
                    Id = Guid.NewGuid(),
                    Street = addr.Street.Trim(),
                    City = addr.City.Trim(),
                    ZipCode = addr.ZipCode.Trim(),
                    Location = addr.Location,
                    AddressType = addr.Type
                }).ToList()
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

            if (!await _entityAuth.CanModifyAsync(userId, company.OwnerId))
            {
                _logger.LogWarning("User {UserId} is not authorized to modify company {CompanyId}.", userId, command.Id);
                return Result.Failure(
                    message: "You are not authorized to modify this company.",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.UnauthorizedAccess
                );
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                var trimmedName = command.Name.Trim();
                if (await _context.Companies.AnyAsync(c => c.Id != command.Id && c.Name.ToLower() == trimmedName.ToLower()))
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
                if (await _context.Companies.AnyAsync(c => c.Id != command.Id && c.NIP == cleanNip))
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

            if (!await _entityAuth.CanModifyAsync(userId, address.Company.OwnerId))
            {
                _logger.LogWarning("User {UserId} is not authorized to modify address {AddressId}.", userId, command.AddressId);
                return Result.Failure(
                    message: "You are not authorized to modify this address.",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.UnauthorizedAccess
                );
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

            await _context.SaveChangesAsync();
            return Result.Success(
                message: "Address updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
