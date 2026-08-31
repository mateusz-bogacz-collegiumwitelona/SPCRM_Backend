namespace Services.Response.Company
{
    public record CompanySimpleListResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
    }
}
