namespace Api.Request.SteelGrade
{
    public record EditSteelGradeRequest
    {
        public required Guid Id { get; init; }
        public string? Name { get; init; }
        public string? Standard { get; init; }
        public decimal? Density { get; init; }
    }
}
