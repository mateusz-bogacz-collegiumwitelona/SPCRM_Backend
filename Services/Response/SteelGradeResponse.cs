namespace Services.Response
{
    public record SteelGradeResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
    }
}
