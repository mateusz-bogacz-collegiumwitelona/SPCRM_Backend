namespace Api.Request.Contact
{
    public record ChangeContactOwnerRequest
    {
        public required Guid NewOwnerId { get; init; }
        public required Guid ContactId { get; init; }
    }
}
