using Domain.Common;
using DTO.Request;
using Services.Response;

namespace Services.Interfaces
{
    public interface IDebtService
    {
        Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid comapnyId);
        Task<Result<PagedResult<CompanyDebtDetailResponse>>> GetCompanyDebtsAsync(Guid companyId, PaggedRequest pagged);
    }
}
