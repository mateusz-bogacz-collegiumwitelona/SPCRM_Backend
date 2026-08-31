namespace Services.Command.Offer
{
    public record ExtendOfferValidityCommand
    {
        public Guid OfferId { get; init; }
        public DateTime? NewValidUntil { get; init; }
    }
}
