using Domain.Common;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ICompanyServices
    {
        Task<Result<List<CompaniesMapResponse>>> Map(string? searchTerm = null);

        Task<Result<CompanyDetailResponse>> Details(string id);
    }
}
