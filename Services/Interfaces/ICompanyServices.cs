using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ICompanyServices
    {
        Task<Result<List<CompaniesMapResponse>>> Map(string? searchTerm = null);

        Task<Result<CompanyDetailResponse>> Details(Guid id, Guid userId);
        Task<Result<PagedResult<AddressDetailResponse>>> GetCompanyAddresses(Guid companyId, PaggedRequest pagged);
        Task<Result<PagedResult<GetCompanyResponse>>> GetCompanyListAsync(Guid userId,
            PaggedRequest pagged, CompanyFilerRequest filer, SearchRequest search
        );
    }
}
