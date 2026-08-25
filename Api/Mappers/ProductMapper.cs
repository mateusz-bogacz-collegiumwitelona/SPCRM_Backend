using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class ProductMapper
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

        [MapProperty(nameof(AddProductRequest.PricePerUnit), nameof(AddProductCommand.PricePerUnit), Use = nameof(MapPriceToDatabase))]
        [MapProperty(nameof(AddProductRequest.Weight), nameof(AddProductCommand.Weight), Use = nameof(MapWeightToGrams))]
        [MapProperty(nameof(AddProductRequest.Thickness), nameof(AddProductCommand.Thickness), Use = nameof(MapDimensionToDatabase))]
        [MapProperty(nameof(AddProductRequest.Width), nameof(AddProductCommand.Width), Use = nameof(MapDimensionToDatabase))]
        [MapProperty(nameof(AddProductRequest.Length), nameof(AddProductCommand.Length), Use = nameof(MapDimensionToDatabase))]
        [MapProperty(nameof(AddProductRequest.Diameter), nameof(AddProductCommand.Diameter), Use = nameof(MapOptionalIntDimensionToDatabase))]
        public partial AddProductCommand MapAdd(AddProductRequest request);

        [MapProperty(nameof(EditProductRequest.PricePerUnit), nameof(EditProductCommand.PricePerUnit), Use = nameof(MapOptionalPriceToDatabase))]
        [MapProperty(nameof(EditProductRequest.Weight), nameof(EditProductCommand.Weight), Use = nameof(MapOptionalWeightToGrams))]
        [MapProperty(nameof(EditProductRequest.Thickness), nameof(EditProductCommand.Thickness), Use = nameof(MapOptionalDecimalDimensionToDatabase))]
        [MapProperty(nameof(EditProductRequest.Width), nameof(EditProductCommand.Width), Use = nameof(MapOptionalDecimalDimensionToDatabase))]
        [MapProperty(nameof(EditProductRequest.Length), nameof(EditProductCommand.Length), Use = nameof(MapOptionalDecimalDimensionToDatabase))]
        [MapProperty(nameof(EditProductRequest.Diameter), nameof(EditProductCommand.Diameter), Use = nameof(MapOptionalDecimalDimensionToDatabase))]
        public partial EditProductCommand MapEdit(EditProductRequest request);

        private long? MapOptionalPriceToDatabase(decimal? price)
            => price.HasValue ? (long)Math.Round(price.Value * 10000m) : null;

        private int? MapOptionalWeightToGrams(decimal? weight)
            => weight.HasValue ? (int)Math.Round(weight.Value * 1000m) : null;

        private long MapPriceToDatabase(decimal price)
            => (long)Math.Round(price * 10000m);

        private int MapWeightToGrams(decimal weight)
            => (int)Math.Round(weight * 1000m);

        private int MapDimensionToDatabase(int dimension)
            => dimension * 10;

        private int? MapOptionalIntDimensionToDatabase(int? dimension)
            => dimension.HasValue ? dimension.Value * 10 : null;

        private int? MapOptionalDecimalDimensionToDatabase(decimal? dimension)
            => dimension.HasValue ? (int)Math.Round(dimension.Value * 10m) : null;
    }
}
