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
        Task<Result<List<CompanySimpleListResponse>>> GetCompanySimpleListAsync();
        Task<Result<Guid>> AddCompanyAsync(AddCompanyCommand command, Guid userId);
        Task<Result> EditCompanyAsync(EditCompanyCommand command, Guid userId);
        Task<Result> EditCompanyAddressAsync(EditCompanyAddressCommand command, Guid userId);
        Task<Result<Guid>> AddCompanyAddressAsync(AddCompanyAdressCommand command, Guid userId, Guid companyId);
        Task<Result> DeleteCompanyAsync(Guid companyId, Guid userId);
        Task<Result> DeleteCompanyAddressAsync(Guid addressId, Guid userId);
        Task<Result> ChangeCompanyOwnerAsync(ChangeCompanyOwnerCommand command);
        Result<List<string>> GetCompanyAddressTypes();
    }
}
