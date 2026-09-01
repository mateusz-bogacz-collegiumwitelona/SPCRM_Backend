using Domain.Common;
using Services.Command.List;
using Services.Command.Offer;
using Services.Response.Offer;

namespace Services.Interfaces
{
    public interface IOfferServices
    {
        Task<Result<PagedResult<OfferListResponse>>> GetOfferListAsync(OfferListCommand command);
        Task<Result<OfferDetailResponse>> GetOfferDetailAsync(Guid id);
        Task<Result<OfferClientDetail>> GetOfferClientDetailAsync(Guid id);
        Task<Result<PagedResult<OfferProductResponse>>> GetOfferProductsAsync(Guid id, SimpleListCommand command);
        Task<Result> ExtendOfferValidityAsync(ExtendOfferValidityCommand command);
        Task<Result<Guid?>> ChangeOfferStatusAsync(ChangeOfferStatusCommand command);
    }
}
