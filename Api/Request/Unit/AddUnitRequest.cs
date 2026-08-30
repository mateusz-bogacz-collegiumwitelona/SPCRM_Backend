namespace Api.Request.Unit
{
    public record AddUnitRequest
    {
        public required string Name { get; set; }
        public required string Symbol { get; set; }
        public required int BaseMultiplier { get; set; }
    }
}
