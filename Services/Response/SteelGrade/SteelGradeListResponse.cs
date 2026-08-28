namespace Services.Response.SteelGrade
{
    public record SteelGradeListResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public string? Standard { get; init; }
        public required decimal Density { get; init; }
    }
}
