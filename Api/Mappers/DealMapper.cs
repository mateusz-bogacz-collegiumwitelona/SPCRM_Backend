using Api.Request.Deal;
using Api.Request.List;
using Api.Request.Sale;
using Riok.Mapperly.Abstractions;
using Services.Command.Deal;

namespace Api.Mappers
{
    [Mapper]
    public partial class DealMapper
    {
        public DealListCommand MapList(
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            DealsFilterRequest filter
            )
            => new DealListCommand
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
                StatusType = filter.StatusType,
                OwnerId = filter.OwnerId
            };

        public partial AddDealCommand MapAdd(AddDealRequest request);
        public partial AddDealProductCommand MapAdd(AddDealProductRequest request);

        public partial ExtendDealCloseDateCommand MapExtendCloseDate(ExtendDealCloseDateRequest request);
    }
}
