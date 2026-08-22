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
        [MapProperty(nameof(AddProductRequest.Diameter), nameof(AddProductCommand.Diameter), Use = nameof(MapOptionalDimensionToDatabase))]
        public partial AddProductCommand MapAdd(AddProductRequest request);

        private long MapPriceToDatabase(decimal price)
            => (long)Math.Round(price * 10000m);

        private int MapWeightToGrams(decimal weight)
            => (int)Math.Round(weight * 1000m); 

        private int MapDimensionToDatabase(int dimension)
            => dimension * 10; 

        private int? MapOptionalDimensionToDatabase(int? dimension)
            => dimension.HasValue ? dimension.Value * 10 : null;
    }
}
