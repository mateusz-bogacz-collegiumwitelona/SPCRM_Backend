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

        public async Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, PaggedRequest pagged)
        {
            try
            {
                var query = _context.Deals
                    .Where(d => d.OwnerId == userId)
                    .Select(d => new UserSalesResponse
                    {
                        Id = d.Id,
                        Name = d.Name,
                        CloseDate = d.CloseDate,
                        Value = (decimal)d.Value / 10000m,
                        DecimalPlace = d.Currency.DecimalPlaces,
                        Currency = d.Currency.Code,
                        CompanyName = d.Company.Name
                    })
                    .ToListAsync();

                var result = await query.ToPagedResultAsync(pagged, _logger, "sales");

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
