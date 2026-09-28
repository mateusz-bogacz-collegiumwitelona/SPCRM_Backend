namespace Api.Request.SteelGrade
{
    public record DeleteSteelGradeRequest
    {
        public List<ProductReassignmentRequest> Reassignments { get; init; } = new();
    }
}
