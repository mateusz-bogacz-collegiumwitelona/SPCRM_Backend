namespace DTO.Response
{
    public record AddressDetailResponse
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string ZipCode { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public required string Type { get; init; }
    }
}
