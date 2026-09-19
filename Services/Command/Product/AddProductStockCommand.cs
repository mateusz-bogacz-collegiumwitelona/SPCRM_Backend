namespace Services.Command.Product
{
    public record AddProductStockCommand
    {
        public required Guid ProductId { get; init; }
        public required int QuantityToAdd { get; init; }
    }
}
