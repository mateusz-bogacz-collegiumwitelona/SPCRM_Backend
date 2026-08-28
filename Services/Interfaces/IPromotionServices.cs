using Domain.Common;
using Services.Command.Promotion;
using Services.Response.Promotion;

namespace Services.Interfaces
{
    public interface IPromotionServices
    {
        Task<Result<PagedResult<PromotionResponse>>> GetPromotionListAsync(PromotionListCommand command);
        Task<Result<PromotionDetailResponse>> GetPromotionDetailAsync(Guid promotionId);
        Task<Result> DeactivatePromotionAsync(Guid promotionId);
        Task<Result> ActivatePromotionAsync(ActivatePromotionCommand command);
        Task<Result> DeletePromotionAsync(Guid promotionId);
        Task<Result> EditPromotionAsync(EditPromotionCommand command);
        Task<Result> AddPromotionAsync(AddPromotionCommand command);
    }
}
