namespace Services.Response
{
    public record UserSalesResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Nip { get; init; }
        public required string Status { get; init; }
        public required DateTime CloseDate { get; init; }
        public required decimal Value { get; init; }
        public required int DecimalPlace { get; init; }
        public required string Currency { get; init; }
        public required string CompanyName { get; init; }
    }
}
