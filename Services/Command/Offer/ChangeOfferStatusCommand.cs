using Domain.Enum;

namespace Services.Command.Offer
{
    public record ChangeOfferStatusCommand
    {
        public Guid OfferId { get; init; }
        public OfferStatusEnum NewStatus { get; init; }
    }
}
