using Api.Request.Offer;
using Domain.Enum;
using Riok.Mapperly.Abstractions;
using Services.Command.Offer;

namespace Api.Mappers
{
    [Mapper]
    public partial class OfferMapper
    {
        [MapProperty(nameof(OfferListRequest.Status), nameof(OfferListCommand.Status), Use = nameof(MapStringToStatus))]
        public partial OfferListCommand MapList(OfferListRequest request);

        public partial ExtendOfferValidityCommand MapExtend(ExtendOfferValidityRequest request);

        [MapProperty(nameof(ChangeOfferStatusRequest.NewStatus), nameof(ChangeOfferStatusCommand.NewStatus), Use = nameof(MapStringToRequiredStatus))]
        public partial ChangeOfferStatusCommand MapChangeStatus(ChangeOfferStatusRequest request);

        public partial UpdateOfferProductsCommand MapUpdateProducts(UpdateOfferProductsRequest request);

        private OfferStatusEnum? MapStringToStatus(string? status)
            => Enum.TryParse<OfferStatusEnum>(status, true, out var parsedStatus) ? parsedStatus : null;

        private OfferStatusEnum MapStringToRequiredStatus(string status)
            => Enum.TryParse<OfferStatusEnum>(status, true, out var parsedStatus)
                ? parsedStatus
                : (OfferStatusEnum)(-1);
    }
}
