namespace Api.Request.Deal
{
    public record EditDealProductRequest
    {
        public required Guid DealProductId { get; init; }
        public int? Quantity { get; init; }
        public long? UnitPrice { get; init; }
    }
}
