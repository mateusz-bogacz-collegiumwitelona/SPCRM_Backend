namespace Api.Request.Company
{
    public record EditCompanyRequest
    {
        public required Guid Id { get; init; }
        public string? Name { get; init; }
        public string? NIP { get; init; }
    }
}
