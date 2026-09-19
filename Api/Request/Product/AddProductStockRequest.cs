namespace Api.Request.Product
{
    public record AddProductStockRequest
    {
        public required int QuantityToAdd { get; init; }
    }
}
