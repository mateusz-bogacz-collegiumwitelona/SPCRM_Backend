using Domain.Common;
using Domain.Enum;
using DTO.Request;
using DTO.Response;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Services
{
    public class SalesServices : ISalesServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SalesServices> _logger;

        public SalesServices(
            AppDbContext context,
            ILogger<SalesServices> logger
            )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(
            Guid userId,
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            SalesFilterRequest filter
            )
        {
            var query = _context.Deals
                .Where(d => d.OwnerId == userId)
                .ApplyFilter(filter)
                .Select(d => new UserSalesResponse
                {
                    Id = d.Id,
                    Name = d.Name,
                    Nip = d.Company.NIP,
                    CloseDate = d.CloseDate,
                    Value = (decimal)d.Value / 10000m,
                    DecimalPlace = d.Currency.DecimalPlaces,
                    Currency = d.Currency.Name,
                    CompanyName = d.Company.Name,
                    Status = d.Status.ToString()
                })
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .ApplySorting(sorting);


            return await query.ToPagedResultAsync(pagged, _logger, "sales");
        }

        public async Task<Result<List<String>>> GetSalesStatus()
        {
            var statuses = Enum.GetNames(typeof(DealsStatusEnum)).ToList();

            return Result<List<string>>.Success(
                message: "Sales statuses retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: statuses
                );
        }

        public async Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(Guid comapnyId, PaggedRequest pagged)
        {
            var query = _context.Deals
                .Where(d => d.CompanyId == comapnyId)
                .Select(d => new CompanySalesResponse
                {
                    Id = d.Id,
                    SalesmanFirstName = d.Owner.FirstName,
                    SalesmanLastName = d.Owner.LastName,
                    Name = d.Name,
                    Value = (decimal)d.Value / 10000m,
                    Code = d.Currency.Code,
                    DecimalPlaces = d.Currency.DecimalPlaces,
                    Status = d.Status.ToString(),
                    CloseDate = d.CloseDate,
                    CreatedAt = d.CreatedAt
                });

            return await query.ToPagedResultAsync(pagged, _logger, "company_sales");
        }
    }
}
