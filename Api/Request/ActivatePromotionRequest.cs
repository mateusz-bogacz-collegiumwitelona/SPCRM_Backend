namespace Api.Request
{
    public record ActivatePromotionRequest
    {
        public required Guid Id { get; init; }
        public required DateTime EndDate { get; init; }
    }
}
