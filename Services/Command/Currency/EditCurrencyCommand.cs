namespace Services.Command.Currency
{
    public record EditCurrencyCommand
    {
        public required Guid CurrencyId { get; init; }
        public string? Name { get; init; }
        public string? Code { get; init; }
        public int? DecimalPlaces { get; init; }
    }
}
