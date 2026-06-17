namespace DTO.Response
{
    public record GetCompanyResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Nip { get; init; }
        public DateTime? LastDealDate { get; init; }
        public bool IsYour { get; init; }
        public string? OwnerFirstName { get; init; }
        public string? OwnerLastName { get; init; }
    }
}
