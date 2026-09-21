namespace Services.Response.Analytics
{
    public record CurrencyAmountResponse
    {
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required decimal Amount { get; init; }
    }
}
