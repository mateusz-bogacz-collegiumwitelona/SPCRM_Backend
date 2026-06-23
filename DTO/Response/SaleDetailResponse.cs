namespace DTO.Response
{
    public record SaleDetailResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required long Value { get; init; } // x10000
        public required string Status { get; init; }
        public required DateTime CloseDate { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
        public required string OwnerFirstName { get; init; }
        public required string OwnerLastName { get; init; }
        public required string CompanyName { get; init; }
    }
}
