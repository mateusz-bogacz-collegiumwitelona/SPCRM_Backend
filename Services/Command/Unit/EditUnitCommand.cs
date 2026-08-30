namespace Services.Command.Unit
{
    public record EditUnitCommand
    {
        public required Guid UnitId { get; init; }
        public string? Name { get; init; }
        public string? Symbol { get; init; }
        public int? BaseMultiplier { get; init; }
    }
}
