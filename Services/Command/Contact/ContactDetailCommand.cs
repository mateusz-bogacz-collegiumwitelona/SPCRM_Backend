namespace Services.Command.Contact
{
    public record ContactDetailCommand
    {
        public required Guid ContactId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<ContactDetailDetailCommand> Details { get; init; }
    }
}
