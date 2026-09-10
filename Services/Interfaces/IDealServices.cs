using Domain.Common;
using Services.Command.Company;
using Services.Command.Product;
using Services.Command.Sales;
using Services.Response.Company;
using Services.Response.Deal;
using Services.Response.Sale;

namespace Services.Interfaces
{
    public interface IDealServices
    {
        Task<Result<PagedResult<UserSalesResponse>>> GetSalesAsync(DealListCommand command, Guid? forcedOwnerId = null);
        Task<Result<List<String>>> GetSalesStatus();
        Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(CompanyCommand command);
        Task<Result<SaleDetailResponse>> GetSaleDetailAsync(Guid dealId, Guid currentUserId);
        Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(Guid dealId, ProductListCommand command, Guid currentUserId);
    }
}
