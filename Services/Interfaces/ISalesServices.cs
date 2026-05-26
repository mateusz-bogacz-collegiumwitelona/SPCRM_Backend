using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface ISalesServices
    {
        Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, PaggedRequest pagged, SearchRequest search, CompanyFilterRequest filter);
        Task<Result<List<String>>> GetSalesStatus();
        Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(Guid comapnyId, PaggedRequest pagged);
    }
}
