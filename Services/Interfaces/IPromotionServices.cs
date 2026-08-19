using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IPromotionServices
    {
        Task<Result<PagedResult<PromotionResponse>>> GetPromotionListAsync(PromotionListCommand command);
        Task<Result<PromotionDetailResponse>> GetPromotionDetailAsync(Guid promotionId);
        Task<Result> DeactivatePromotionAsync(Guid promotionId);

    }
}
