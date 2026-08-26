namespace Api.Request
{
    public record AddSteelGradeRequest
    {
        public required string Name { get; init; }
        public string? Standard { get; init; }
        public required decimal Density { get; init; }
    }
}
