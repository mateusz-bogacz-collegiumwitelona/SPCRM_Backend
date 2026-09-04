using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
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

            var productData = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    SteelGradeId = p.SteelGradeId,
                    SteelGradeName = p.SteelGrade != null ? p.SteelGrade.Name : null,
                    p.Category,
                    p.Diameter,
                    p.Thickness,
                    p.Width,
                    p.Length,
                    p.StockQuantity,
                    p.Weight,
                    p.PricePerUnit,
                    UnitId = p.UnitId,
                    UnitSymbol = p.Unit != null ? p.Unit.Symbol : null,
                    CurrencyId = p.CurrencyId,
                    CurrencyCode = p.Currency != null ? p.Currency.Code : null,
                    DecimalPlaces = p.Currency != null ? (int?)p.Currency.DecimalPlaces : null,

                    ReservedQuantity = p.DealProducts
                        .Where(dp => dp.Deal.Status == DealsStatusEnum.ToDo || dp.Deal.Status == DealsStatusEnum.InProgress)
                        .Sum(dp => (int?)dp.Quantity) ?? 0,

                    ActivePromotion = p.Promotions
                        .Where(pr => pr.IsActive &&
                                     (!pr.StartDate.HasValue || pr.StartDate <= now) &&
                                     (!pr.EndDate.HasValue || pr.EndDate >= now))
                        .OrderByDescending(pr => pr.DiscountPercentage ?? 0)
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

            if (productData == null)
            {
                _logger.LogInformation("Product with ID {ProductId} not found.", productId);
                return Result<ProductDetailResponse>.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (productData.PricePerUnit < 0 ||
                productData.Weight < 0 ||
                productData.StockQuantity < 0 ||
                productData.UnitId == Guid.Empty ||
                productData.CurrencyId == Guid.Empty ||
                productData.SteelGradeId == Guid.Empty ||
                string.IsNullOrWhiteSpace(productData.CurrencyCode) ||
                string.IsNullOrWhiteSpace(productData.UnitSymbol) ||
                string.IsNullOrWhiteSpace(productData.SteelGradeName))
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has invalid pricing, weight or missing dictionary relations.", productId);
                throw new DataCorruptionException($"Product '{productId}' contains corrupted state or missing dictionary linkage.");
            }

            var formattedDimensions = DimensionsFormatter.Format(
                productData.Category,
                productData.Diameter,
                productData.Thickness,
                productData.Width,
                productData.Length
            );

            var response = new ProductDetailResponse
            {
                Id = productData.Id,
                Name = productData.Name,
                SteelGrade = productData.SteelGradeName,
                Category = productData.Category.ToString(),
                Dimensions = formattedDimensions,
                StockQuantity = productData.StockQuantity,
                UnitSymbol = productData.UnitSymbol,
                PricePerUnit = productData.PricePerUnit,
                CurrencyCode = productData.CurrencyCode,
                DecimalPlaces = productData.DecimalPlaces!.Value,
                Weight = productData.Weight,
                ReservedQuantity = productData.ReservedQuantity,
                ActivePromotion = productData.ActivePromotion
            };

            return Result<ProductDetailResponse>.Success(
                message: "Product details retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
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
            if (!Enum.TryParse<ProductCategoryEnum>(command.Category, true, out var category))
            {
                _logger.LogWarning("Invalid product category provided: {Category}", command.Category);
                return Result.Failure(
                    message: "Invalid product category.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidCategory
                );
            }

            string trimName = command.Name.Trim();

            var nameExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.Name == trimName);

            if (nameExists)
            {
                _logger.LogWarning("Attempt to add a product with an existing name: {ProductName}", trimName);
                return Result.Failure(
                    message: "Product with the same name already exists.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.ProductAlreadyExists
                );
            }

            var unitExists = await _context.UnitsOfMeasure
                .AsNoTracking()
                .AnyAsync(u => u.Id == command.UnitId);

            if (!unitExists)
            {
                _logger.LogWarning("Unit of measure with ID {UnitId} not found.", command.UnitId);
                return Result.Failure(
                    message: "Unit of measure not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            var steelGrade = await _context.SteelGrades
                .FirstOrDefaultAsync(sg => sg.Id == command.SteelGradeId);

            if (steelGrade == null)
            {
                _logger.LogWarning("Steel grade with ID {SteelGradeId} not found.", command.SteelGradeId);
                return Result.Failure(
                    message: "Steel grade not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            var currency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Id == command.CurrencyId);

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
                Id = Guid.NewGuid(),
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

            _logger.LogInformation("Product {ProductId} ('{ProductName}') created successfully.", product.Id, product.Name);

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
                _logger.LogInformation("Product with ID {ProductId} not found for editing.", command.ProductId);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (product.PricePerUnit < 0 ||
                product.Weight < 0 ||
                product.StockQuantity < 0 ||
                product.UnitId == Guid.Empty ||
                product.CurrencyId == Guid.Empty ||
                product.SteelGradeId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has invalid pricing/weight or missing dictionary foreign keys.", product.Id);
                throw new DataCorruptionException($"Product '{product.Id}' contains corrupted state.");
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                string trimName = command.Name.Trim();
                var nameConflict = await _context.Products
                    .AsNoTracking()
                    .AnyAsync(p => p.Name == trimName && p.Id != command.ProductId);

                if (nameConflict)
                {
                    _logger.LogWarning("Attempt to edit product {ProductId} with an existing name: {ProductName}", command.ProductId, trimName);
                    return Result.Failure(
                        message: "Another product with the same name already exists.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.ProductAlreadyExists
                    );
                }

                product.Name = trimName;
            }

            if (command.Thickness.HasValue) product.Thickness = command.Thickness.Value;
            if (command.Width.HasValue) product.Width = command.Width.Value;
            if (command.Length.HasValue) product.Length = command.Length.Value;
            if (command.Diameter.HasValue) product.Diameter = command.Diameter.Value;
            if (command.Weight.HasValue) product.Weight = command.Weight.Value;
            if (command.PricePerUnit.HasValue) product.PricePerUnit = command.PricePerUnit.Value;
            if (command.StockQuantity.HasValue) product.StockQuantity = command.StockQuantity.Value;

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
                var unitExists = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == command.UnitId.Value);

                if (!unitExists)
                {
                    _logger.LogWarning("Unit of measure with ID {UnitId} not found.", command.UnitId.Value);
                    return Result.Failure(
                        message: "Unit of measure not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }

                product.UnitId = command.UnitId.Value;
            }

            if (command.SteelGradeId.HasValue)
            {
                var steelGradeExists = await _context.SteelGrades
                    .AsNoTracking()
                    .AnyAsync(s => s.Id == command.SteelGradeId.Value);

                if (!steelGradeExists)
                {
                    _logger.LogWarning("Steel grade with ID {SteelGradeId} not found.", command.SteelGradeId.Value);
                    return Result.Failure(
                        message: "Steel grade not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }

                product.SteelGradeId = command.SteelGradeId.Value;
            }

            if (command.CurrencyId.HasValue)
            {
                var currencyExists = await _context.Currencies
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == command.CurrencyId.Value);

                if (!currencyExists)
                {
                    _logger.LogWarning("Currency with ID {CurrencyId} not found.", command.CurrencyId.Value);
                    return Result.Failure(
                        message: "Currency not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.NotFound
                    );
                }

                product.CurrencyId = command.CurrencyId.Value;
            }

            product.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} updated successfully.", product.Id);

            return Result.Success(
                message: "Product updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<EditProductDetailResponse>> GetProductEditDetailAsync(Guid id)
        {
            var rawProduct = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SteelGradeId,
                    p.UnitId,
                    p.CurrencyId,
                    p.Category,
                    p.Thickness,
                    p.Width,
                    p.Length,
                    p.Diameter,
                    p.Weight,
                    p.PricePerUnit,
                    p.StockQuantity,
                    CurrencyExists = p.Currency != null
                })
                .FirstOrDefaultAsync();

            if (rawProduct == null)
            {
                _logger.LogInformation("Product with ID {ProductId} not found for edit.", id);
                return Result<EditProductDetailResponse>.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (rawProduct.PricePerUnit < 0 ||
                rawProduct.Weight < 0 ||
                rawProduct.StockQuantity < 0 ||
                rawProduct.Thickness < 0 ||
                rawProduct.Width < 0 ||
                rawProduct.Length < 0 ||
                (rawProduct.Diameter.HasValue && rawProduct.Diameter.Value < 0) ||
                rawProduct.UnitId == Guid.Empty ||
                rawProduct.CurrencyId == Guid.Empty ||
                rawProduct.SteelGradeId == Guid.Empty ||
                !rawProduct.CurrencyExists)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} contains negative values or unlinked dictionaries.", id);
                throw new DataCorruptionException($"Product '{id}' has corrupted state or missing dictionary linkage.");
            }

            var response = new EditProductDetailResponse
            {
                ProductId = rawProduct.Id,
                Name = rawProduct.Name,
                SteelGradeId = rawProduct.SteelGradeId,
                UnitId = rawProduct.UnitId,
                CurrencyId = rawProduct.CurrencyId,
                Category = rawProduct.Category.ToString(),

                Thickness = rawProduct.Thickness / 10m,
                Width = rawProduct.Width / 10m,
                Length = rawProduct.Length / 10m,
                Diameter = rawProduct.Diameter.HasValue ? rawProduct.Diameter.Value / 10m : null,
                Weight = rawProduct.Weight / 1000m,
                PricePerUnit = rawProduct.PricePerUnit / 10000m,
                StockQuantity = rawProduct.StockQuantity
            };

            return Result<EditProductDetailResponse>.Success(
                message: "Product details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result> DeleteProductAsync(Guid id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                _logger.LogInformation("Product with ID {ProductId} not found.", id);
                return Result.Failure(
                    message: "Product not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound
                );
            }

            if (product.CurrencyId == Guid.Empty ||
                product.SteelGradeId == Guid.Empty ||
                product.UnitId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has corrupted dictionary relations.", id);
                throw new DataCorruptionException($"Product '{id}' contains corrupted foreign key linkage.");
            }

            var hasActiveDeals = await _context.DealProducts
                .AsNoTracking()
                .AnyAsync(dp => dp.ProductId == id &&
                                (dp.Deal.Status == DealsStatusEnum.ToDo || dp.Deal.Status == DealsStatusEnum.InProgress));

            if (hasActiveDeals)
            {
                _logger.LogWarning("Attempt to delete product {ProductId} ('{ProductName}') which is currently locked in active deals.", product.Id, product.Name);
                return Result.Failure(
                    message: "Cannot delete product that is assigned to active deals.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} ('{ProductName}') deleted successfully.", product.Id, product.Name);

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

            var safeLimit = command.Limit <= 0 ? 10 : Math.Clamp(command.Limit, 1, 50);
            var searchPattern = $"%{trimmedQuery}%";

            var rawProducts = await _context.Products
                .AsNoTracking()
                .Where(p =>
                    EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(searchPattern)) ||
                    (p.SteelGrade != null && EF.Functions.ILike(EF.Functions.Unaccent(p.SteelGrade.Name), EF.Functions.Unaccent(searchPattern)))
                )
                .OrderBy(p => p.Name)
                .Take(safeLimit)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SteelGradeId,
                    SteelGradeName = p.SteelGrade != null ? p.SteelGrade.Name : null,
                    p.PricePerUnit
                })
                .ToListAsync();

            var corrupted = rawProducts.FirstOrDefault(p => p.PricePerUnit < 0 || p.SteelGradeId == Guid.Empty || string.IsNullOrWhiteSpace(p.SteelGradeName));
            if (corrupted != null)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has negative price or missing steel grade.", corrupted.Id);
                throw new DataCorruptionException($"Product '{corrupted.Id}' has corrupted pricing or missing steel grade linkage.");
            }

            var response = rawProducts.Select(p => new ProductAutocompleteResponse
            {
                Id = p.Id,
                Name = p.Name,
                SteelGrade = p.SteelGradeName!,
                PricePerUnit = p.PricePerUnit
            }).ToList();

            return Result<List<ProductAutocompleteResponse>>.Success(
                message: "Products retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }
    }
}
