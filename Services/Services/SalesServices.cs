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
    public class SalesServices : ISalesServices
    {
        public readonly AppDbContext _context;
        private readonly ILogger<SalesServices> _logger;

        public SalesServices(
            AppDbContext context,
            ILogger<SalesServices> logger
            )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, PaggedRequest pagged, CompanyFilterRequest filter)
        {
            try
            {
                var query = _context.Deals
                    .Where(d => d.OwnerId == userId)
                    .ApplyFilter(filter, pagged.SearchTerm)
                    .Select(d => new UserSalesResponse
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Nip = d.Company.NIP,
                        CloseDate = d.CloseDate,
                        Value = (decimal)d.Value / 10000m,
                        DecimalPlace = d.Currency.DecimalPlaces,
                        Currency = d.Currency.Name,
                        CompanyName = d.Company.Name
                    })
                    .ApplySorting(pagged);

                var result = await query.ToListAsync().ToPagedResultAsync(pagged, _logger, "sales");

                return result;
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
    }
}
