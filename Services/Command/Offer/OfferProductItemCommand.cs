namespace Services.Command.Offer
{
    public record OfferProductItemCommand
    {
        public Guid ProductId { get; init; }
        public int Quantity { get; init; }
        public long QuotedPrice { get; init; } // x10000
    }
}
