using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SalesMapper
    {
        public partial SalesListCommand MapList(
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            SalesFilterRequest filter
            );
    }
}
