namespace Services.Command
{
    public record MailingProductCommand
    {
        public required Guid ProductId { get; init; }
        public string? CurrencyCode { get; init; }
        public long? Price { get; init; }

        public required int Quantity { get; init; }
    }
}
