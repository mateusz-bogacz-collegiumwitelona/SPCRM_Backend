namespace DTO.Response
{
    public record CompanyDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Nip { get; init; }
        public required List<AddressDetailResponse> Addresses { get; init; }
    }
}
