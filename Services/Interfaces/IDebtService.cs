using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IDebtService
    {
        Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid comapnyId);
        Task<Result<PagedResult<CompanyDebtDetailResponse>>> GetCompanyDebtsAsync(CompanyCommand command);
    }
}
