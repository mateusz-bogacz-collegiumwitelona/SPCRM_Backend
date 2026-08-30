namespace Services.Command.Unit
{
    public record AddUnitCommand
    {
        public required string Name { get; init; }
        public required string Symbol { get; init; }
        public required int BaseMultiplier { get; init; }
    }
}
