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

        private OfferStatusEnum? MapStringToStatus(string? status)
            => Enum.TryParse<OfferStatusEnum>(status, true, out var parsedStatus) ? parsedStatus : null;
    }
}
