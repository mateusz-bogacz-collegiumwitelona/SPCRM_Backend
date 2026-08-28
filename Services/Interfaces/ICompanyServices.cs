using Domain.Common;
using Services.Command.Company;
using Services.Response.Company;

namespace Services.Interfaces
{
    public interface ICompanyServices
    {
        Task<Result<List<CompaniesMapResponse>>> Map(string? searchTerm = null);

        Task<Result<CompanyDetailResponse>> Details(Guid id, Guid userId);
        Task<Result<PagedResult<AddressDetailResponse>>> GetCompanyAddresses(CompanyCommand command);
        Task<Result<PagedResult<CompanyResponse>>> GetCompanyListAsync(CompanyListCommand command);
    }
}
