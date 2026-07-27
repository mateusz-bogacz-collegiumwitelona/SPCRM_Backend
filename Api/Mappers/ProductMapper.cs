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
                SteelGrade = filter.SteelGrade
            };
    }
}
