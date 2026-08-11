namespace Services.Command
{
    public record EditContactCommand
    {
        public required Guid ContactId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<EditContactDetailCommand> Details { get; init; }
    }
}
