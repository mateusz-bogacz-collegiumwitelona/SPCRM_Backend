namespace Services.Response.Unit
{
    public record UnitSimpleListResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Symbol { get; init; }
    }
}
