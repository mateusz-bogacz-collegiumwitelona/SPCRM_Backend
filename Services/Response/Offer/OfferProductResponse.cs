namespace Services.Response.Offer
{
    public record OfferProductResponse
    {
        public required Guid ProductId { get; init; }
        public required string ProductName { get; init; }
        public required string SteelGrade { get; init; }
        public int Quantity { get; init; }
        public long QuotedPrice { get; init; }
        public required string CurrencyCode { get; init; }
        public int DecimalPlaces { get; init; }
    }
}
