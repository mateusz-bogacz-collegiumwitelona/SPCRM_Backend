namespace Services.Response
{
    public record TaskDealResponse
    {
        public required Guid DealId { get; init; }
        public required string Name { get; init; }
        public required long Value { get; init; } 
        public required string Status { get; init; }
        public required DateTime CloseDate { get; init; }
        public required string CurrencyCode { get; init; }
        public required int DecimalPlaces { get; init; }
    }
}
