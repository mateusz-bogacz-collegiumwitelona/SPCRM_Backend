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
            try
            {
                var query = _context.CompanyAdresses.Where(a => !a.IsDeleted);

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
            catch (Exception ex)
            {
                return Result<List<CompaniesMapResponse>>.Failure(
                   "An error occurred while get data for map",
                   ErrorCodes.InternalError,
                   StatusCodes.Status500InternalServerError,
                   new List<string> { ex.Message }
                   );
            }
        }

        public async Task<Result<CompanyDetailResponse>> Details(string id, Guid userId)
        {
            try
            {
                if (!Guid.TryParse(id, out Guid parsedId))
                {
                    return Result<CompanyDetailResponse>.Failure(
                        message: "Invalid ID format",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.BadRequest
                    );
                }


                var company = await _context.Companies
                    .FirstAsync(c => c.Id.ToString() == id);

                if (company == null || company.IsDeleted)
                {
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
            catch (Exception ex)
            {
                return Result<CompanyDetailResponse>.Failure(
                   "An error occurred while get data for map",
                   ErrorCodes.InternalError,
                   StatusCodes.Status500InternalServerError,
                   new List<string> { ex.Message }
                   );
            }
        }

        public async Task<Result<PagedResult<AddressDetailResponse>>> GetCompanyAddresses(
            Guid companyId, 
            PaggedRequest pagged
            )
        {
            try
            {
                var query = _context.CompanyAdresses
                    .Where(a => a.CompanyId == companyId)
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

                return await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "comapny_adresses");
            } 
            catch (Exception ex) 
            {
                return Result<PagedResult<AddressDetailResponse>>.Failure(
                    message: "An error occurred while fetching company addresses.",
                    errorCode: ErrorCodes.InternalError,
                    statusCode: StatusCodes.Status500InternalServerError,
                    errors: new List<string> { ex.Message }
                );
            }
        }
    }
}
