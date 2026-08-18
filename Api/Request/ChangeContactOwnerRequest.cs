namespace Api.Request
{
    public record ChangeContactOwnerRequest
    {
        public required Guid NewOwnerId { get; init; }
        public required Guid ContactId { get; init; }
    }
}
