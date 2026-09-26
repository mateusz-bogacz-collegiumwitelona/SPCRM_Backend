namespace Api.Request.Company
{
    public class AddCompanyRequest
    {
        public required string Name { get; init; }
        public required string NIP { get; init; }
        public required List<AddCompanyAdressesRequest> Addresses { get; init; } = new();
    }
}
