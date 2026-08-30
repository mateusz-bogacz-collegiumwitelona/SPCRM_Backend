namespace Services.Command.Unit
{
    public record AddUnitCommand
    {
        public required string Name { get; set; }
        public required string Symbol { get; set; }
        public required int BaseMultiplier { get; set; }
    }
}
