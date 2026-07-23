using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.Response;

namespace Services.Services
{
    public class ProductSevices : IProductSevices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ProductSevices> _logger;

        public ProductSevices(AppDbContext context, ILogger<ProductSevices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(ProductListCommand command)
        {
            var query = _context.Products
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplyFilter(command.ProductCategory, command.SteelGrade)
                .ApplySorting(command.SortBy ?? string.Empty, command.SortDescending)
                .Select(p => new ProductResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SteelGrade = p.SteelGrade,
                    Category = p.Category.ToString(),

                    Dimensions = DimensionsFormatter.Format(
                     p.Category,
                     p.Diameter,
                     p.Thickness,
                     p.Width,
                     p.Length
                    ),

                    StockQuantity = p.StockQuantity,
                    UnitSymbol = p.Unit.Symbol
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "products");
        }

        public async Task<Result<IEnumerable<string>>> GetProductCategoryAsync()
        {
            var query = Enum.GetNames(typeof(ProductCategoryEnum)).ToList();

            return Result<IEnumerable<string>>.Success(
                message: "Product categories reviewed successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<IEnumerable<string>>> GetSteelGradesAsync()
        {
            var query = await _context.Products
                .AsNoTracking()
                .Select(p => p.SteelGrade)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            return Result<IEnumerable<string>>.Success(
                message: "Product categories reviewed successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<ProductDetailResponse>> GetProductDetailsAsync(Guid productId)
        {
            var query = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new ProductDetailResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SteelGrade = p.SteelGrade,
                    Category = p.Category.ToString(),

                    Dimensions = DimensionsFormatter.Format(
                     p.Category,
                     p.Diameter,
                     p.Thickness,
                     p.Width,
                     p.Length
                    ),

                    StockQuantity = p.StockQuantity,
                    UnitSymbol = p.Unit.Symbol,
                    PricePerUnit = (decimal)p.PricePerUnit / 10000m,
                    Weight = (decimal) p.Weight / 1000m,

                    ReservedQuantity = p.DealProducts
                        .Where(dp => dp.Deal.Status == DealsStatusEnum.ToDo || dp.Deal.Status == DealsStatusEnum.InProgress)
                        .Sum(dp => (int?)dp.Quantity) ?? 0
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found.", productId);
                return Result<ProductDetailResponse>.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound.ToString()
                    );
            }

            return Result<ProductDetailResponse>.Success(
                message: "Product details retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }
    }
}
