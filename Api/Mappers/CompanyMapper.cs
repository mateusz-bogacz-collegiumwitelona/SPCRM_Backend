using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class CompanyMapper
    {
        public CompanyCommand MapBasic(Guid companyId, PaggedRequest request)
            => new CompanyCommand
            {
                CompanyId = companyId,
                PageNumber = request?.PageNumber,
                PageSize = request?.PageSize
            };

        public CompanyListCommand MapList(
            Guid userId,
            PaggedRequest pagged,
            CompanyFilterRequest filter,
            SortingRequest sorting,
            SearchRequest search)
            => new CompanyListCommand
            {
                UserId = userId,
                PageNumber = pagged?.PageNumber,
                PageSize = pagged?.PageSize,
                IsYour = filter?.IsYour,
                CreatedAtFrom = filter?.CreatedAtFrom,
                CreatedAtTo = filter?.CreatedAtTo,
                SortBy = sorting?.SortBy,
                SortDescending = sorting?.SortDescending ?? false,
                SearchTerm = search?.SearchTerm
            };

    }
}
