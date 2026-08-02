namespace Api.Request
{
    public record MailingRequest
    {
        public required List<Guid> To { get; init; }
        public required List<MailingProductRequest> Products { get; init; }
        public required string Language { get; init; }
    }
}
