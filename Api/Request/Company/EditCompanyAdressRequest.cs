namespace Api.Request.Company
{
    public class EditCompanyAdressRequest
    {
        public required Guid AddressId { get; init; }
        public string? Street { get; init; }
        public string? City { get; init; }
        public string? ZipCode { get; init; }
        public float? Longitude { get; init; }
        public float? Latitude { get; init; }
        public string? Type { get; init; }
    }
}
