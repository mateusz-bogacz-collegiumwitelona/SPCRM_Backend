using Domain.Common;
using Domain.Constants;
using Domain.Enum;
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
            var now = DateTime.UtcNow;

            var query = _context.Products
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplyFilter(command.ProductCategory, command.SteelGrade, command.HasActivePromotion)
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
                    UnitSymbol = p.Unit.Symbol,

                    IsActivePromotion = p.Promotions.Any(pr =>
                        pr.IsActive &&
                        (!pr.StartDate.HasValue || pr.StartDate <= now) &&
                        (!pr.EndDate.HasValue || pr.EndDate >= now)
                        )
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
            var now = DateTime.UtcNow;

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
                    PricePerUnit = p.PricePerUnit,
                    Weight = p.Weight,

                    ReservedQuantity = p.DealProducts
                        .Where(dp => dp.Deal.Status == DealsStatusEnum.ToDo || dp.Deal.Status == DealsStatusEnum.InProgress)
                        .Sum(dp => (int?)dp.Quantity) ?? 0,

                    ActivePromotion = p.Promotions
                        .Where(pr => pr.IsActive && (!pr.StartDate.HasValue || pr.StartDate <= now) && (!pr.EndDate.HasValue || pr.EndDate >= now))
                        .Select(pr => new ActivePromotionResponse
                        {
                            Name = pr.Name,
                            DiscountPercentage = pr.DiscountPercentage,
                            PromotionalPrice = pr.PromotionalPrice,
                            EndDate = pr.EndDate,
                            MinQuantity = pr.MinQuantity
                        })
                        .FirstOrDefault()
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

        public async Task<Result<PagedResult<MailingProductResponse>>> GetMailingProductsAsync(SimpleListCommand command)
        {
            var now = DateTime.UtcNow;

            var query = _context.Products
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .Select(p => new MailingProductResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Dimmension = DimensionsFormatter.Format(
                     p.Category,
                     p.Diameter,
                     p.Thickness,
                     p.Width,
                     p.Length
                    ),
                    StockQuantity = p.StockQuantity,
                    StockPrice = (long)p.PricePerUnit,

                    PromotionalPrice = p.Promotions
                        .Where(pr => pr.IsActive &&
                                     (!pr.StartDate.HasValue || pr.StartDate <= now) &&
                                     (!pr.EndDate.HasValue || pr.EndDate >= now))
                        .Select(pr => pr.PromotionalPrice.HasValue
                            ? (long?)pr.PromotionalPrice.Value
                            : (long?)(p.PricePerUnit * (1 - (pr.DiscountPercentage ?? 0) / 100m)))
                        .FirstOrDefault()
                });
            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "mailing products");
        }

        public async Task<Result> AddProductAsync(AddProductCommand command)
        {
            string trimName = command.Name.Trim();

            if (await _context.Products.AnyAsync(p => p.Name == trimName))
            {
                _logger.LogWarning("Attempt to add a product with an existing name: {ProductName}", command.Name);
                return Result.Failure(
                    message: "Product with the same name already exists.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.ProductAlreadyExists
                );
            }

            var unit = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == command.UnitId);

            if (unit == null)
            {
                _logger.LogWarning("Unit of measure with ID {UnitId} not found.", command.UnitId);
                return Result.Failure(
                    message: "Unit of measure not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }
            if (!Enum.TryParse<ProductCategoryEnum>(command.Category, true, out var category))
            {
                _logger.LogWarning("Invalid product category provided: {Category}", command.Category);
                return Result.Failure(
                    message: "Invalid product category.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidCategory
                );
            }

            var product = new Product
            {
                Name = trimName,
                SteelGrade = command.SteelGrade.Trim(),
                Thickness = command.Thickness,
                Width = command.Width,
                Length = command.Length,
                Diameter = command.Diameter,
                Weight = command.Weight,
                UnitId = command.UnitId,
                PricePerUnit = command.PricePerUnit,
                StockQuantity = command.StockQuantity,
                Category = category
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Product added successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }
    }
}
