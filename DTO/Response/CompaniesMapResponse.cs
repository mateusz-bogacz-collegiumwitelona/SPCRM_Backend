namespace DTO.Response
{
    public record CompaniesMapResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Nip { get; init; }
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string ZipCode { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public required string Type { get; init; }
    }
}
