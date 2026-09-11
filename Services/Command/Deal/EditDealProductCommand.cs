namespace Services.Command.Deal
{
    public record EditDealProductCommand
    {
        public required Guid DealProductId { get; init; }
        public int? Quantity { get; init; }
        public long? UnitPrice { get; init; }
    }
}
