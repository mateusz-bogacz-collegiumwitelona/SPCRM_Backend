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

        public CompanyServices(AppDbContext context, ILogger<CompanyServices> logger)
        {
            _context = context;
            _logger = logger;
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
    }
}
