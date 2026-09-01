namespace Services.Command.Offer
{
    public record UpdateOfferProductsCommand
    {
        public Guid OfferId { get; init; }
        public List<OfferProductItemCommand> Items { get; init; } = new();
    }
}
