using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.List;
using Services.Command.Product;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Product;
using Services.Response.Promotion;

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
                    SteelGrade = p.SteelGrade.Name,
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
                    SteelGrade = p.SteelGrade.Name,
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
                    CurrencyCode = p.Currency.Code,
                    DecimalPlaces = p.Currency.DecimalPlaces,

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

            var steelGrade = await _context.SteelGrades.FirstOrDefaultAsync(sg => sg.Id == command.SteelGradeId);
            if (steelGrade == null)
            {
                _logger.LogWarning("Steel grade with ID {SteelGradeId} not found.", command.SteelGradeId);
                return Result.Failure(
                    message: "Steel grade not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == command.CurrencyId);
            if (currency == null)
            {
                _logger.LogWarning("Currency with ID {CurrencyId} not found.", command.CurrencyId);
                return Result.Failure(
                    message: "Currency not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            var product = new Product
            {
                Name = trimName,
                SteelGrade = steelGrade,
                SteelGradeId = steelGrade.Id,
                Thickness = command.Thickness,
                Width = command.Width,
                Length = command.Length,
                Diameter = command.Diameter,
                Weight = command.Weight,
                UnitId = command.UnitId,
                PricePerUnit = command.PricePerUnit,
                CurrencyId = currency.Id,
                Currency = currency,
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

        public async Task<Result> EditProductAsync(EditProductCommand command)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == command.ProductId);

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found for editing.", command.ProductId);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                string trimName = command.Name.Trim();
                if (await _context.Products.AnyAsync(p => p.Name == trimName && p.Id != command.ProductId))
                {
                    _logger.LogWarning("Attempt to edit product with an existing name: {ProductName}", command.Name);
                    return Result.Failure(
                        message: "Another product with the same name already exists.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.ProductAlreadyExists
                    );
                }
                product.Name = trimName;
            }

            if (command.Thickness.HasValue)
            {
                product.Thickness = command.Thickness.Value;
            }

            if (command.Width.HasValue)
            {
                product.Width = command.Width.Value;
            }

            if (command.Length.HasValue)
            {
                product.Length = command.Length.Value;
            }

            if (command.Diameter.HasValue)
            {
                product.Diameter = command.Diameter.Value;
            }

            if (command.Weight.HasValue)
            {
                product.Weight = command.Weight.Value;
            }

            if (command.PricePerUnit.HasValue)
            {
                product.PricePerUnit = command.PricePerUnit.Value;
            }

            if (command.StockQuantity.HasValue)
            {
                product.StockQuantity = command.StockQuantity.Value;
            }

            if (!string.IsNullOrWhiteSpace(command.Category))
            {
                if (Enum.TryParse<ProductCategoryEnum>(command.Category, true, out var category))
                {
                    product.Category = category;
                }
                else
                {
                    _logger.LogWarning("Invalid product category provided for editing: {Category}", command.Category);
                    return Result.Failure(
                        message: "Invalid product category.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidCategory
                    );
                }
            }


            if (command.UnitId.HasValue)
            {
                var unit = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == command.UnitId.Value);
                if (unit == null)
                {
                    _logger.LogWarning("Unit of measure with ID {UnitId} not found.", command.UnitId.Value);
                    return Result.Failure(
                        message: "Unit of measure not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }
                product.UnitId = unit.Id;
            }

            if (command.SteelGradeId.HasValue)
            {
                var steelGrade = await _context.SteelGrades.FirstOrDefaultAsync(s => s.Id == command.SteelGradeId.Value);
                if (steelGrade == null)
                {
                    _logger.LogWarning("Steel grade with ID {SteelGradeId} not found.", command.SteelGradeId.Value);
                    return Result.Failure(
                        message: "Steel grade not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }
                product.SteelGradeId = steelGrade.Id;
            }

            if (command.CurrencyId.HasValue)
            {
                var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == command.CurrencyId.Value);
                if (currency == null)
                {
                    _logger.LogWarning("Currency with ID {CurrencyId} not found.", command.CurrencyId.Value);
                    return Result.Failure(
                        message: "Currency not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }
                product.CurrencyId = currency.Id;
            }

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Product updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<EditProductDetailResponse>> GetProductEditDetailAsync(Guid id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new EditProductDetailResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SteelGradeId = p.SteelGradeId,
                    UnitId = p.UnitId,
                    CurrencyId = p.CurrencyId,
                    Category = p.Category.ToString(),

                    Thickness = p.Thickness / 10m,
                    Width = p.Width / 10m,
                    Length = p.Length / 10m,
                    Diameter = p.Diameter.HasValue ? p.Diameter.Value / 10m : null,
                    Weight = p.Weight / 1000m,
                    PricePerUnit = p.PricePerUnit / 10000m,
                    StockQuantity = p.StockQuantity
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found for edit.", id);
                return Result<EditProductDetailResponse>.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            return Result<EditProductDetailResponse>.Success(
                message: "Product details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: product
            );
        }

        public async Task<Result> DeleteProductAsync(Guid id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                _logger.LogWarning("Product with this id {id} not found.", id);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                    );
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Product deleted successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result<List<ProductAutocompleteResponse>>> SearchProductsAutocompleteAsync(SearchProductAutocompleteCommand command)
        {
            var trimmedQuery = command.Query?.Trim() ?? string.Empty;

            if (trimmedQuery.Length < 2)
            {
                return Result<List<ProductAutocompleteResponse>>.Success(
                    message: "Query is too short. At least 2 characters required.",
                    statusCode: StatusCodes.Status200OK,
                    data: new List<ProductAutocompleteResponse>()
                );
            }

            var safeLimit = Math.Clamp(command.Limit, 1, 50);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && (
                    EF.Functions.ILike(p.Name, $"%{trimmedQuery}%") ||
                    (p.SteelGrade != null && EF.Functions.ILike(p.SteelGrade.Name, $"%{trimmedQuery}%"))
                ))
                .OrderBy(p => p.Name)
                .Take(safeLimit)
                .Select(p => new ProductAutocompleteResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    SteelGrade = p.SteelGrade != null ? p.SteelGrade.Name : string.Empty,
                    PricePerUnit = p.PricePerUnit
                })
                .ToListAsync();

            return Result<List<ProductAutocompleteResponse>>.Success(
                message: "Products retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: products
            );
        }
    }
}
