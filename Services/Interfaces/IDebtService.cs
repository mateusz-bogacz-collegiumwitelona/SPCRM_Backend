using Domain.Common;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IDebtService
    {
        Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid comapnyId);
    }
}
