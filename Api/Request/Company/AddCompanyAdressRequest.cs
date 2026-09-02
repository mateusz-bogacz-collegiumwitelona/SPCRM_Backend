namespace Api.Request.Company
{
    public class AddCompanyAdressRequest
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string ZipCode { get; init; }
        public required float Longitude { get; init; }
        public required float Latitude { get; init; }
        public required string Type { get; init; }
    }
}
