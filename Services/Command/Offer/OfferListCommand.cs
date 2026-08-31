using Domain.Enum;

namespace Services.Command.Offer
{
    public record OfferListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public DateTime? ValidUntilFrom { get; init; }
        public DateTime? ValidUntilTo { get; init; }
        public string? CompanyName { get; init; }
        public OfferStatusEnum? Status { get; init; } = null;
        public string? SearchTerm { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public bool? IsExpired { get; init; }
    }
}
