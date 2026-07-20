using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface ISalesServices
    {
        Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(Guid userId, SalesListCommand command);
        Task<Result<List<String>>> GetSalesStatus();
        Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(CompanyCommand command);
        Task<Result<SaleDetailResponse>> GetSaleDetailAsync(Guid dealId);
        Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(Guid dealId, ProductListCommand command);
        Task<Result<List<NoteResponse>>> GetDealNotesAsync(Guid dealId);
    }
}
