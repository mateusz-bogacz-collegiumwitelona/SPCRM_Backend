using Domain.Common;
using Domain.Constants;
using Domain.Enum;
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
            SearchRequest search,
            CompanyFilterRequest filter
            )
        {
            try
            {
                var query = _context.Deals
                    .Where(d => d.OwnerId == userId)
                    .ApplyFilter(filter, search.SearchTerm ?? string.Empty)
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
                    .ApplySorting(search);

                
                return await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "sales");
            }
            catch (Exception ex)
            {
                return Result<PagedResult<UserSalesResponse>>.Failure(
                    message: "An error occurred while retrieving user sales",
                    statusCode: StatusCodes.Status500InternalServerError,
                    errorCode: ErrorCodes.InternalError,
                    errors: new List<string> { ex.Message }
                    );
            }
        }

        public async Task<Result<List<String>>> GetSalesStatus()
        {
            try
            {
                var statuses = Enum.GetNames(typeof(DealsStatusEnum)).ToList();
                return Result<List<string>>.Success(
                    message: "Sales statuses retrieved successfully",
                    statusCode: StatusCodes.Status200OK,
                    data: statuses
                    );

            }
            catch (Exception ex)
            {
                return Result<List<string>>.Failure(
                    message: "An error occurred while retrieving sales statuses",
                    statusCode: StatusCodes.Status500InternalServerError,
                    errorCode: ErrorCodes.InternalError,
                    errors: new List<string> { ex.Message }
                    );
            }
        }
    }
}
