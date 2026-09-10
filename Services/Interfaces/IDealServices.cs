using Domain.Common;
using Services.Command.Company;
using Services.Command.Product;
using Services.Command.Sales;
using Services.Response.Company;
using Services.Response.Deal;

namespace Services.Interfaces
{
    public interface IDealServices
    {
        Task<Result<PagedResult<UserDealResponse>>> GetDealsAsync(DealListCommand command, Guid? forcedOwnerId = null);
        Task<Result<List<String>>> GetDealsStatus();
        Task<Result<PagedResult<CompanyDealsResponse>>> GetComapanyDealsAsync(CompanyCommand command);
        Task<Result<DealDetailResponse>> GetDealDetailAsync(Guid dealId, Guid currentUserId);
        Task<Result<PagedResult<DealProductResponse>>> GetSaleProductAsync(Guid dealId, ProductListCommand command, Guid currentUserId);
    }
}
