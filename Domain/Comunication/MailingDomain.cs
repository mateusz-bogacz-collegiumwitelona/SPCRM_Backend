namespace Domain.Comunication
{
    public record MailingOfferDomain
    {
        public required List<string> BccEmails { get; init; }
        public required string Language { get; init; }
        public required List<MailingProductItemDomain> Products { get; init; }
    }

    public record MailingProductItemDomain
    {
        public required string ProductName { get; init; }
        public required string SteelGrade { get; init; }
        public required string FormattedDimensions { get; init; }
        public required int Weight { get; init; }
        public required string UnitSymbol { get; init; }
        public required int Quantity { get; init; }
        public required string CurrencyCode { get; init; }

        public required long FinalPrice { get; init; }

        public long? OriginalPrice { get; init; }
        public decimal? DiscountPercentage { get; init; }
        public bool IsPromoted { get; init; }
    }
}
