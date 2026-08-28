namespace Api.Request.SteelGrade
{
    public record DeleteSteelGradeRequest
    {
        public List<ProductReassignmentRequest> Reassignments { get; init; } = new();
    }

    public record ProductReassignmentRequest
    {
        public required Guid ProductId { get; init; }
        public required Guid NewSteelGradeId { get; init; }
    }
}
