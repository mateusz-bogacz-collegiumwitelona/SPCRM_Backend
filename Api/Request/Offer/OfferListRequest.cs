using Api.Request.List;

namespace Api.Request.Offer
{
    public record OfferListRequest : BasicListRequest
    {
        public DateTime? ValidUntilFrom { get; init; }
        public DateTime? ValidUntilTo { get; init; }
        public string? CompanyName { get; init; }
        public string? Status { get; init; } = null;
        public bool? IsExpired { get; init; }

    }
}
