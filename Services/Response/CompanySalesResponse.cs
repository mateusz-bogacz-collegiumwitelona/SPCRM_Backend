namespace Services.Response
{
    public record CompanySalesResponse
    {
        public required Guid Id { get; init; }
        public required string SalesmanFirstName { get; init; }
        public required string SalesmanLastName { get; init; }
        public required string Name { get; init; }
        public required decimal Value { get; init; } // x10000
        public required string Code { get; init; }
        public int DecimalPlaces { get; init; }
        public required string Status { get; init; }
        public DateTime CloseDate { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
