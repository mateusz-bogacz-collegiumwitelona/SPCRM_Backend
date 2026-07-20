using Domain.Common;
using DTO.Request;
using Services.Response;

namespace Services.Interfaces
{
    public interface ISalesServices
    {
        Task<Result<PagedResult<UserSalesResponse>>> GetUserSales(
            Guid userId,
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            SalesFilterRequest filter
            );
        Task<Result<List<String>>> GetSalesStatus();
        Task<Result<PagedResult<CompanySalesResponse>>> GetComapanySalesAsync(Guid comapnyId, PaggedRequest pagged);
        Task<Result<SaleDetailResponse>> GetSaleDetailAsync(Guid dealId);
        Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(
            Guid dealId,
            PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
        );
        Task<Result<List<NoteResponse>>> GetDealNotesAsync(Guid dealId);
    }
}
