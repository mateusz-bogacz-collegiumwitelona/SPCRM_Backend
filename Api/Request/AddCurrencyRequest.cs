namespace Api.Request
{
    public record AddCurrencyRequest
    {
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required int DecimalPlaces { get; init; }
    }
}
