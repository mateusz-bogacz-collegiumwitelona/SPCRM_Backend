namespace Api.Request.Company
{
    public class AddCompanyRequest
    {
        public required string Name { get; init; }
        public required string NIP { get; init; }
        public required List<AddCompanyAdressRequest> Address { get; init; } = new();
    }
}
