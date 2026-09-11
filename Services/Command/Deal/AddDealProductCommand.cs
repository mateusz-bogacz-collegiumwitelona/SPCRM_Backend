namespace Services.Command.Deal
{
    public record AddDealProductCommand
    {
        public required Guid ProductId { get; init; }
        public required int Quantity { get; init; }
        public required long UnitPrice { get; init; }

    }
}
