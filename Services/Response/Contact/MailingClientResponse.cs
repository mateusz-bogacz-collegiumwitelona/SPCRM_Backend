namespace Services.Response.Contact
{
    public record MailingClientResponse
    {
        public required string CompanyName { get; init; }
        public required string Nip { get; init; }
        public required string ContactFirstName { get; init; }
        public required string ContactLastName { get; init; }
        public required Guid ContactId { get; init; }
    }
}
