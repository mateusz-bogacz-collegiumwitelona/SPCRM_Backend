namespace Services.Command.Offer
{
    public record ResendOfferEmailCommand
    {
        public Guid OfferId { get; init; }
        public string? Language { get; init; }
    }
}
