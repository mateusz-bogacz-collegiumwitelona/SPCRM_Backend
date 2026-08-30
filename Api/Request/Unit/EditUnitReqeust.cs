namespace Api.Request.Unit
{
    public record EditUnitReqeust
    {
        public required Guid UnitId { get; init; }
        public string? Name { get; init; }
        public string? Symbol { get; init; }
        public int? BaseMultiplier { get; init; }
    }
}
