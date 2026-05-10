using Domain.Common;
using Domain.Constants;
using DTO.Response;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class CompanyServices : ICompanyServices
    {
        private readonly AppDbContext _context;

        public CompanyServices(AppDbContext context)
        {
            _context = context;
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
    }
}
