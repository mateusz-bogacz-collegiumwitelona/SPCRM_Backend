namespace Services.Response
{
    public record CurrencyListResponse
    {
        public required Guid CurrencyId { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required int DecimalPlace { get; init; }

    }
}
