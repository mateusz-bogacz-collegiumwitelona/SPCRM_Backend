namespace Api.Request.Deal
{
    public record AddDealRequest
    {
        public required DateTime CloseDate { get; init; }
        public required Guid CurrencyId { get; init; }
        public required Guid CompanyId { get; init; }
        public required List<AddDealProductRequest> Products { get; init; } = new();
    }
}
