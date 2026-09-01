namespace Services.Response.Offer
{
    public record OfferAllowedActionsResponse
    {
        public bool CanEdit { get; init; }
        public bool CanDelete { get; init; }
        public bool CanResendEmail { get; init; }
        public bool CanExtendValidity { get; init; }
        public List<string> AllowedStatusTransitions { get; init; } = new();
    }
}
