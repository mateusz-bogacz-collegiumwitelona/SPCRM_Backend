namespace Services.Command
{
    public record AddCurrencyCommand
    {
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required int DecimalPlaces { get; init; }
    }
}
