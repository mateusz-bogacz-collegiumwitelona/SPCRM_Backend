using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using Services.Response;

namespace Services.Services
{
    public class CurrencyServices : ICurrencyServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CurrencyServices> _logger;

        public CurrencyServices(AppDbContext context, ILogger<CurrencyServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<CurrencySimpleListResponse>>> GetCurrencySimpleListAsync()
        {
            var query = await _context.Currencies
                .Select(c => new CurrencySimpleListResponse
                {
                    CurrencyId = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    DecimalPlace = c.DecimalPlaces
                }).ToListAsync();

            return Result<List<CurrencySimpleListResponse>>.Success(
               message: "Currency list retrieved successfully.",
               statusCode: StatusCodes.Status200OK,
               data: query
               );
        }
    }
}
