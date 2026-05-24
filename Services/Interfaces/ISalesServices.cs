using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ISalesServices
    {
        Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, PaggedRequest pagged, CompanyFilterRequest filter);
        Task<Result<List<String>>> GetSalesStatus();
    }
}
