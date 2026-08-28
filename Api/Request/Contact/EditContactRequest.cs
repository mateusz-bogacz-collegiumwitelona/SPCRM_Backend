namespace Api.Request.Contact
{
    public record EditContactRequest
    {
        public required Guid ContactId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? JobTitle { get; init; }
        public required List<EditContactDetailRequest> Details { get; init; }

    }
}
