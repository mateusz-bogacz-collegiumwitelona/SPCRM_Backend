using Domain.Common;
using Services.Command.Offer;
using Services.Response;

namespace Services.Interfaces
{
    public interface IOfferServices
    {
        Task<Result<PagedResult<OfferListResponse>>> GetOfferListAsync(OfferListCommand command);
    }
}
