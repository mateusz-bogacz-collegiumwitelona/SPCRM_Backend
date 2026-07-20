using Domain.Common;
using DTO.Request;
using Services.Response;

namespace Services.Interfaces
{
    public interface ICompanyServices
    {
        Task<Result<List<CompaniesMapResponse>>> Map(string? searchTerm = null);

        Task<Result<CompanyDetailResponse>> Details(Guid id, Guid userId);
        Task<Result<PagedResult<AddressDetailResponse>>> GetCompanyAddresses(Guid companyId, PaggedRequest pagged);
        Task<Result<PagedResult<CompanyResponse>>> GetCompanyListAsync(Guid userId,
            PaggedRequest pagged, CompanyFilterRequest filer, SortingRequest sorting, SearchRequest search
            );
    }
}
