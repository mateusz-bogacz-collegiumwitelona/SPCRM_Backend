using Domain.Common;
using Services.Command.Offer;
using Services.Response.Offer;

namespace Services.Interfaces
{
    public interface IOfferServices
    {
        Task<Result<PagedResult<OfferListResponse>>> GetOfferListAsync(OfferListCommand command);
        Task<Result<OfferDetailResponse>> GetOfferDetailAsync(Guid id);
    }
}
