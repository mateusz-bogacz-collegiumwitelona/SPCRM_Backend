namespace Api.Request.Promotion
{
    public record ActivatePromotionRequest
    {
        public required Guid Id { get; init; }
        public required DateTime EndDate { get; init; }
    }
}
