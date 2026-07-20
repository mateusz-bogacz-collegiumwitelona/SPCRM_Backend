using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class CompanyMapper
    {
        public partial CompanyCommand MapBasic(Guid companyId, PaggedRequest request);
        public partial CompanyListCommand MapList(
            Guid userId,
            PaggedRequest pagged,
            CompanyFilterRequest filter,
            SortingRequest sorting,
            SearchRequest search
            );
    }
}
