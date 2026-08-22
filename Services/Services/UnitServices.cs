using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using Services.Response;

namespace Services.Services
{
    public class UnitServices : IUnitServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UnitServices> _logger;

        public UnitServices(AppDbContext context, ILogger<UnitServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<UnitListResponse>>> GetSimpleUnitList()
        {
            var query = await _context.UnitsOfMeasure
                .Select(uom => new UnitListResponse
                {
                    Id = uom.Id,
                    Name = uom.Name,
                    Symbol = uom.Symbol
                })
                .ToListAsync();

            return Result<List<UnitListResponse>>.Success(
                data: query,
                message: "Review all unit of mesure",
                statusCode: StatusCodes.Status200OK
                );
        }
    }
}
