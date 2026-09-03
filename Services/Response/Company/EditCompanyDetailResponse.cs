namespace Services.Response.Company
{
    public record EditCompanyDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string NIP { get; init; }
    }
}
