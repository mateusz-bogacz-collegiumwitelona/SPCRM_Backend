namespace Api.Request.Deal
{
    public record AddDealProductRequest
    {
        public required Guid ProductId { get; init; }
        public required int Quantity { get; init; }
        public required long UnitPrice { get; init; }
    }
}
