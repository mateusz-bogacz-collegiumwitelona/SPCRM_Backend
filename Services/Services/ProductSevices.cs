using Domain.Common;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        public async Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
            )
        {
            var query = _context.Products
                .AsNoTracking()
                .ApplySearch(search.SearchTerm ?? string.Empty)
                .ApplyFilter(filter)
                .ApplySorting(sorting)
                .Select(p => new ProductResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SteelGrade = p.SteelGrade,
                    Category = p.ProductCategory.Name,

                    Dimensions = DimensionsFormatter.Format(
                     p.ProductCategory.Category,
                     p.Diameter,
                     p.Thickness,
                     p.Width,
                     p.Length
                    ),

                    StockQuantity = p.StockQuantity,
                    UnitSymbol = p.Unit.Symbol
                });

            return await query.ToPagedResultAsync(pagged, _logger, "products");
        }

        public async Task<Result<IEnumerable<string>>> GetProductCategoryAsync()
        {
            var query = await _context.ProductCategories
                .AsNoTracking()
                .Select(pc => pc.Name)
                .Distinct()
                .OrderBy(pc => pc)
                .ToListAsync();

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
    }
}
