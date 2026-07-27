using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SalesMapper
    {
        public SalesListCommand MapList(
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            SalesFilterRequest filter
            )
            => new SalesListCommand
            {
                PageNumber = pagged.PageNumber,
                PageSize = pagged.PageSize,
                SortBy = sorting.SortBy,
                SortDescending = sorting.SortDescending,
                SearchTerm = search.SearchTerm,
                CompanyName = filter.CompanyName,
                Value = filter.Value,
                DateFrom = filter.DateFrom,
                DateTo = filter.DateTo,
                StatusType = filter.StatusType
            };
    }
}
