namespace Services.Command.Contact
{
    public record AddContactCommand
    {
        public required Guid CompanyId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<AddContactDetailCommand> Details { get; init; }
    }
}
