namespace Services.Command.SteelGrade
{
    public record AddSteelGradeCommand
    {
        public required string Name { get; init; }
        public string? Standard { get; init; }
        public required int Density { get; init; }
    }
}
