using Domain.Common;
using Domain.Constants;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.List;
using Services.Command.Unit;
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

        public async Task<Result> AddUnitAsync(AddUnitCommand command)
        {
            var isExist = await _context.UnitsOfMeasure.AnyAsync(uom =>
                uom.Name.ToLower() == command.Name.ToLower().Trim() ||
                uom.Symbol.ToLower() == command.Symbol.ToLower().Trim()
            );

            if (isExist)
            {
                _logger.LogWarning("Unit with name {Name} or symbol {Symbol} already exists.", command.Name, command.Symbol);
                return Result.Failure(
                    message: "Unit with the same name or symbol already exists.",
                    statusCode: StatusCodes.Status409Conflict,
                    errorCode: ErrorCodes.UnitAlreadyExists
                    );
            }

            var unit = new UnitOfMeasure
            {
                Name = command.Name,
                Symbol = command.Symbol,
                BaseMultiplier = command.BaseMultiplier
            };

            _context.UnitsOfMeasure.Add(unit);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Unit added successfully.",
                statusCode: StatusCodes.Status201Created
                );
        }

        public async Task<Result> EditUnitAsync(EditUnitCommand command)
        {
            var unit = await _context.UnitsOfMeasure.FindAsync(command.UnitId);

            if (unit == null)
            {
                _logger.LogWarning("Unit with ID {UnitId} not found.", command.UnitId);
                return Result.Failure(
                    message: "Unit not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UnitNotFound
                    );
            }

            if (!string.IsNullOrEmpty(command.Name))
            {
                var isNameExist = await _context.UnitsOfMeasure.AnyAsync(uom =>
                    uom.Id != command.UnitId &&
                    uom.Name.ToLower() == command.Name.ToLower().Trim()
                );

                if (isNameExist)
                {
                    _logger.LogWarning("Unit with name {Name} already exists.", command.Name);
                    return Result.Failure(
                        message: "Unit with the same name already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.UnitAlreadyExists
                        );
                }

                unit.Name = command.Name;
            }

            if (!string.IsNullOrEmpty(command.Symbol))
            {
                var isSymbolExist = await _context.UnitsOfMeasure.AnyAsync(uom =>
                    uom.Id != command.UnitId &&
                    uom.Symbol.ToLower() == command.Symbol.ToLower().Trim()
                );

                if (isSymbolExist)
                {
                    _logger.LogWarning("Unit with symbol {Symbol} already exists.", command.Symbol);
                    return Result.Failure(
                        message: "Unit with the same symbol already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.UnitAlreadyExists
                        );
                }

                unit.Symbol = command.Symbol;
            }

            if (command.BaseMultiplier.HasValue)
            {
                unit.BaseMultiplier = command.BaseMultiplier.Value;
            }

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Unit updated successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }
    }
}
