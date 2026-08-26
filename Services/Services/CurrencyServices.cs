using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
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

        public async Task<Result<List<CurrencyListResponse>>> GetCurrencySimpleListAsync()
        {
            var query = await _context.Currencies
                .Select(c => new CurrencyListResponse
                {
                    CurrencyId = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    DecimalPlace = c.DecimalPlaces
                }).ToListAsync();

            return Result<List<CurrencyListResponse>>.Success(
               message: "Currency list retrieved successfully.",
               statusCode: StatusCodes.Status200OK,
               data: query
               );
        }

        public async Task<Result<PagedResult<CurrencyListResponse>>> GetCurrenyListAsync(BasicListCommand command)
            => await _context.Currencies
                    .AsNoTracking()
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(c => new CurrencyListResponse
                    {
                        CurrencyId = c.Id,
                        Name = c.Name,
                        Code = c.Code,
                        DecimalPlace = c.DecimalPlaces
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "currency");
    }
}
