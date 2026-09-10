namespace Services.Response.Deal
{
    public record UserDealResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Nip { get; init; }
        public required string Status { get; init; }
        public required DateTime CloseDate { get; init; }
        public required long Value { get; init; }
        public required int DecimalPlace { get; init; }
        public required string Currency { get; init; }
        public required string CompanyName { get; init; }

        public Guid OwnerId { get; init; }
        public string OwnerFirstName { get; init; } = null!;
        public string OwnerLastName { get; init; } = null!;
    }
}
