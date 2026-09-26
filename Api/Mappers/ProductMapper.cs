using Api.Mappers.Helper;
using Api.Request.List;
using Api.Request.Product;
using Riok.Mapperly.Abstractions;
using Services.Command.Product;

namespace Api.Mappers
{
    [Mapper]
    public partial class ProductMapper : BaseMapper
    {
        public ProductListCommand MapList(
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
            )
            => new ProductListCommand
            {
                PageNumber = pagged.PageNumber,
                PageSize = pagged.PageSize,
                SortBy = sorting.SortBy,
                SortDescending = sorting.SortDescending,
                SearchTerm = search.SearchTerm,
                ProductCategory = filter.ProductCategory,
                SteelGrade = filter.SteelGrade,
                HasActivePromotion = filter.HasActivePromotion
            };

        [MapProperty(nameof(AddProductRequest.Name), nameof(AddProductCommand.Name), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddProductRequest.Category), nameof(AddProductCommand.Category), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddProductRequest.Thickness), nameof(AddProductCommand.Thickness), Use = nameof(MapDecimalDimension))]
        [MapProperty(nameof(AddProductRequest.Width), nameof(AddProductCommand.Width), Use = nameof(MapDecimalDimension))]
        [MapProperty(nameof(AddProductRequest.Length), nameof(AddProductCommand.Length), Use = nameof(MapDecimalDimension))]
        [MapProperty(nameof(AddProductRequest.Diameter), nameof(AddProductCommand.Diameter), Use = nameof(MapOptionalDecimalDimension))]
        [MapProperty(nameof(AddProductRequest.PricePerUnit), nameof(AddProductCommand.PricePerUnit), Use = nameof(MapPrice))]
        [MapProperty(nameof(AddProductRequest.Weight), nameof(AddProductCommand.Weight), Use = nameof(MapWeightToGrams))]
        public partial AddProductCommand MapAdd(AddProductRequest request);

        [MapProperty(nameof(EditProductRequest.Name), nameof(EditProductCommand.Name), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditProductRequest.Category), nameof(EditProductCommand.Category), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditProductRequest.PricePerUnit), nameof(EditProductCommand.PricePerUnit), Use = nameof(MapOptionalPrice))]
        [MapProperty(nameof(EditProductRequest.Weight), nameof(EditProductCommand.Weight), Use = nameof(MapOptionalWeightToGrams))]
        [MapProperty(nameof(EditProductRequest.Thickness), nameof(EditProductCommand.Thickness), Use = nameof(MapOptionalDecimalDimension))]
        [MapProperty(nameof(EditProductRequest.Width), nameof(EditProductCommand.Width), Use = nameof(MapOptionalDecimalDimension))]
        [MapProperty(nameof(EditProductRequest.Length), nameof(EditProductCommand.Length), Use = nameof(MapOptionalDecimalDimension))]
        [MapProperty(nameof(EditProductRequest.Diameter), nameof(EditProductCommand.Diameter), Use = nameof(MapOptionalDecimalDimension))]
        public partial EditProductCommand MapEdit(EditProductRequest request);

        public partial SearchProductAutocompleteCommand MapSearch(SearchProductAutocompleteRequest request);
        public AddProductStockCommand MapAddStock(AddProductStockRequest request, Guid productId)
            => new AddProductStockCommand
            {
                ProductId = productId,
                QuantityToAdd = request.QuantityToAdd
            };

        private int MapDecimalDimension(decimal dimension)
            => (int)Math.Round(dimension * 10m);

        private long? MapOptionalPrice(decimal? price)
            => price.HasValue ? (long)Math.Round(price.Value * 10000m) : null;

        private int? MapOptionalWeightToGrams(decimal? weight)
            => weight.HasValue ? (int)Math.Round(weight.Value * 1000m) : null;

        private long MapPrice(decimal price)
            => (long)Math.Round(price * 10000m);

        private int MapWeightToGrams(decimal weight)
            => (int)Math.Round(weight * 1000m);

        private int MapDimension(int dimension)
            => dimension * 10;

        private int? MapOptionalDecimalDimension(decimal? dimension)
            => dimension.HasValue ? (int)Math.Round(dimension.Value * 10m) : null;
    }
}
