namespace Api.Request.Currency
{
    public record EditCurrencyRequest
    {
        public required Guid CurrencyId { get; init; }
        public string? Name { get; init; }
        public string? Code { get; init; }
        public int? DecimalPlaces { get; init; }
    }
}
