namespace Services.Response.Product
{
    public record ProductSimpleResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Category { get; init; }
    }
}
