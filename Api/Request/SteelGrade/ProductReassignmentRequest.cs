namespace Api.Request.SteelGrade
{
    public record ProductReassignmentRequest
    {
        public required Guid ProductId { get; init; }
        public required Guid NewSteelGradeId { get; init; }
    }
}
