using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
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
            var trimmedName = command.Name.Trim();
            var trimmedSymbol = command.Symbol.Trim();

            var isExist = await _context.UnitsOfMeasure
                .AsNoTracking()
                .AnyAsync(uom =>
                    EF.Functions.ILike(uom.Name, trimmedName) ||
                    EF.Functions.ILike(uom.Symbol, trimmedSymbol)
                );

            if (isExist)
            {
                _logger.LogWarning("Unit with name '{Name}' or symbol '{Symbol}' already exists.", trimmedName, trimmedSymbol);
                return Result.Failure(
                    message: "Unit with the same name or symbol already exists.",
                    statusCode: StatusCodes.Status409Conflict,
                    errorCode: ErrorCodes.UnitAlreadyExists
                );
            }

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                Symbol = trimmedSymbol,
                BaseMultiplier = command.BaseMultiplier
            };

            _context.UnitsOfMeasure.Add(unit);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Unit {UnitId} ('{Name}', '{Symbol}') added successfully.", unit.Id, unit.Name, unit.Symbol);

            return Result.Success(
                message: "Unit added successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }

        public async Task<Result> EditUnitAsync(EditUnitCommand command)
        {
            var unit = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == command.UnitId);

            if (unit == null)
            {
                _logger.LogInformation("Unit with ID {UnitId} not found.", command.UnitId);
                return Result.Failure(
                    message: "Unit not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UnitNotFound
                );
            }

            if (string.IsNullOrWhiteSpace(unit.Name) || string.IsNullOrWhiteSpace(unit.Symbol))
            {
                _logger.LogError("Critical data corruption: UnitOfMeasure {UnitId} has empty Name or Symbol.", unit.Id);
                throw new DataCorruptionException($"Unit of measure '{unit.Id}' contains corrupted state.");
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                var trimmedName = command.Name.Trim();
                var isNameExist = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .AnyAsync(uom =>
                        uom.Id != command.UnitId &&
                        EF.Functions.ILike(uom.Name, trimmedName)
                    );

                if (isNameExist)
                {
                    _logger.LogWarning("Unit with name '{Name}' already exists.", trimmedName);
                    return Result.Failure(
                        message: "Unit with the same name already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.UnitAlreadyExists
                    );
                }

                unit.Name = trimmedName;
            }

            if (!string.IsNullOrWhiteSpace(command.Symbol))
            {
                var trimmedSymbol = command.Symbol.Trim();
                var isSymbolExist = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .AnyAsync(uom =>
                        uom.Id != command.UnitId &&
                        EF.Functions.ILike(uom.Symbol, trimmedSymbol)
                    );

                if (isSymbolExist)
                {
                    _logger.LogWarning("Unit with symbol '{Symbol}' already exists.", trimmedSymbol);
                    return Result.Failure(
                        message: "Unit with the same symbol already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.UnitAlreadyExists
                    );
                }

                unit.Symbol = trimmedSymbol;
            }

            if (command.BaseMultiplier.HasValue)
            {
                unit.BaseMultiplier = command.BaseMultiplier.Value;
            }

            unit.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Unit {UnitId} updated successfully.", unit.Id);

            return Result.Success(
                message: "Unit updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
