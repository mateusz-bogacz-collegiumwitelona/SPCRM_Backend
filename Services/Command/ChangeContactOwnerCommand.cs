namespace Services.Command
{
    public record ChangeContactOwnerCommand
    {
        public required Guid NewOwnerId { get; init; }
        public required Guid ContactId { get; init; }
    }
}
