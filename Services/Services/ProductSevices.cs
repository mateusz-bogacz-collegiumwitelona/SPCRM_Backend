using Domain.Common;
using DTO.Request;
using DTO.Response;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Interfaces;

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

                    Dimensions = DimensionsFormatter.Format(
                         p.ProductType.Category.Name,
                         p.ProductType.Name,
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
    }
}
