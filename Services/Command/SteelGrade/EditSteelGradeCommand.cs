namespace Services.Command.SteelGrade
{
    public record EditSteelGradeCommand
    {
        public required Guid Id { get; init; }
        public string? Name { get; init; }
        public string? Standard { get; init; }
        public int? Density { get; init; }
    }
}
