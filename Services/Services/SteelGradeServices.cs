using Domain.Common;
using Domain.Constants;
using Domain.Models;
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
    public class SteelGradeServices : ISteelGradeServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SteelGradeServices> _logger;

        public SteelGradeServices(AppDbContext context, ILogger<SteelGradeServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync()
        {
            var query = await _context.SteelGrades
                .OrderBy(s => s.Name)
                .Select(s => new SteelGradeResponse
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .ToListAsync();

            return Result<IEnumerable<SteelGradeResponse>>.Success(
                message: "Steel grades retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<PagedResult<SteelGradeListResponse>>> GetSteelGradeListAsync(BasicListCommand command)
            => await _context.SteelGrades
                    .AsNoTracking()
                    .ApplySeatch(command.SearchTerm ?? string.Empty)
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(st => new SteelGradeListResponse
                    {
                        Id = st.Id,
                        Name = st.Name,
                        Standard = st.Standard,
                        Density = st.Density / 1000m
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "steel-grade");

        public async Task<Result<List<ProductSimpleResponse>>> GetAssociatedProductsAsync(Guid steelGradeId)
        {
            var products = await _context.Products
                .Where(p => p.SteelGradeId == steelGradeId)
                .Select(p => new ProductSimpleResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category.ToString()
                })
                .ToListAsync();

            return Result<List<ProductSimpleResponse>>.Success(
                message: "Products retrieved",
                statusCode: StatusCodes.Status200OK,
                data: products
            );
        }

        public async Task<Result> DeleteSteelGradeAsync(Guid id, List<ProductReassignmentCommand>? reassignments)
        {
            var steelGrade = await _context.SteelGrades.FirstOrDefaultAsync(s => s.Id == id);
            if (steelGrade == null)
            {
                _logger.LogWarning("Attempted to delete non-existent steel grade with ID: {SteelGradeId}", id);
                return Result.Failure(
                    message: "Steel grade not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            var affectedProducts = await _context.Products
                .Where(p => p.SteelGradeId == id)
                .ToListAsync();

            if (affectedProducts.Count > 0)
            {
                reassignments ??= new List<ProductReassignmentCommand>();

                var missingProductIds = affectedProducts
                    .Select(p => p.Id)
                    .Except(reassignments.Select(r => r.ProductId))
                    .ToList();

                if (missingProductIds.Count > 0)
                {
                    _logger.LogWarning("Not all products have a choice of new steel grade. Missing product IDs: {MissingProductIds}", string.Join(", ", missingProductIds));
                    return Result.Failure(
                        message: $"Not all products have a choice of new steel grade (remaining: {missingProductIds.Count}).",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.SteelGradeInUse
                    );
                }

                if (reassignments.Any(r => r.NewSteelGradeId == id))
                {
                    _logger.LogWarning("The target species cannot be the species being removed. Steel grade ID: {SteelGradeId}", id);
                    return Result.Failure(
                        message: "The target species cannot be the species being removed.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.BadRequest
                    );
                }

                var targetGradeIds = reassignments.Select(r => r.NewSteelGradeId).Distinct().ToList();

                var existingGradesCount = await _context.SteelGrades
                    .CountAsync(s => targetGradeIds.Contains(s.Id));

                if (existingGradesCount != targetGradeIds.Count)
                {
                    _logger.LogWarning("One or more selected target steel grades do not exist.");
                    return Result.Failure(
                        message: "One or more selected target steel grades do not exist.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.BadRequest
                    );
                }

                var reassignmentMap = reassignments.ToDictionary(r => r.ProductId, r => r.NewSteelGradeId);
                foreach (var product in affectedProducts)
                {
                    if (reassignmentMap.TryGetValue(product.Id, out var newGradeId))
                    {
                        product.SteelGradeId = newGradeId;
                    }
                }
            }

            _context.SteelGrades.Remove(steelGrade);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "The steel grade has been removed, and the related products have been updated.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditSteelGradeAsync(EditSteelGradeCommand command)
        {
            var steelGrade = await _context.SteelGrades.FirstOrDefaultAsync(s => s.Id == command.Id);

            if (steelGrade == null)
            {
                _logger.LogWarning("Attempted to edit non-existent steel grade with ID: {SteelGradeId}", command.Id);
                return Result.Failure(
                    message: "Steel grade not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                string normalizedName = command.Name.Trim().ToUpper();

                if (steelGrade.Name != normalizedName)
                {
                    var isExist = await _context.SteelGrades
                        .AnyAsync(s => s.Id != command.Id && s.Name == normalizedName);

                    if (isExist)
                    {
                        _logger.LogWarning("Steel grade with this name: {name} already exists", normalizedName);
                        return Result.Failure(
                            message: "Steel grade with this name already exists",
                            statusCode: StatusCodes.Status400BadRequest,
                            errorCode: ErrorCodes.SteelGradeAlreadyExist
                        );
                    }

                    steelGrade.Name = normalizedName;
                }
            }

            if (command.Standard != null)
            {
                steelGrade.Standard = string.IsNullOrWhiteSpace(command.Standard)
                    ? null
                    : command.Standard.Trim();
            }

            if (command.Density.HasValue)
            {
                steelGrade.Density = command.Density.Value;
            }

            await _context.SaveChangesAsync();
            return Result.Success(
                message: "Steel grade updated successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> AddSteelGradeAsync(AddSteelGradeCommand command)
        {
            string normalizedName = command.Name.Trim().ToUpper();

            var isExist = await _context.SteelGrades
                       .AnyAsync(s => s.Name == normalizedName);

            if (isExist)
            {
                _logger.LogWarning("Steel grade with this name: {name} already exist", normalizedName);
                return Result.Failure(
                    message: "Steel grade with this name already exist",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.SteelGradeAlreadyExist
                );
            }

            var steelGrade = new SteelGrade
            {
                Name = normalizedName,
                Standard = string.IsNullOrWhiteSpace(command.Standard) ? null : command.Standard.Trim(),
                Density = command.Density
            };

            _context.SteelGrades.Add(steelGrade);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Steel grade added successfully",
                statusCode: StatusCodes.Status201Created
            );
        }
    }
}
