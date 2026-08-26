namespace Services.Command
{
    public record ProductReassignmentCommand
    {
        public required Guid ProductId { get; init; }
        public required Guid NewSteelGradeId { get; init; }
    }
}
