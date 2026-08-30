using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.List;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Unit;

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

        public async Task<Result<List<UnitSimpleListResponse>>> GetSimpleUnitList()
        {
            var query = await _context.UnitsOfMeasure
                .Select(uom => new UnitSimpleListResponse
                {
                    Id = uom.Id,
                    Name = uom.Name,
                    Symbol = uom.Symbol
                })
                .ToListAsync();

            return Result<List<UnitSimpleListResponse>>.Success(
                data: query,
                message: "Review all unit of mesure",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result<PagedResult<UnitListResponse>>> GetUnitListAsync(BasicListCommand command)
            => await _context.UnitsOfMeasure
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(uom => new UnitListResponse
                {
                    Id = uom.Id,
                    Name = uom.Name,
                    Symbol = uom.Symbol,
                    BaseMultiplier = uom.BaseMultiplier
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "unit-of-mesure");
    }
}
