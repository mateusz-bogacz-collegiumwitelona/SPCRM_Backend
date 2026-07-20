using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class ProductMapper
    {
        public partial ProductListCommand MapList(
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
            );
    }
}
