using Domain.Enum;
using Services.Command.List;

namespace Services.Command.Offer
{
    public record OfferListCommand : BasicListCommand
    {
        public DateTime? ValidUntilFrom { get; init; }
        public DateTime? ValidUntilTo { get; init; }
        public string? CompanyName { get; init; }
        public OfferStatusEnum? Status { get; init; } = null;
        public bool? IsExpired { get; init; }
    }
}
