namespace Services.Command.Deal
{
    public record ExtendDealCloseDateCommand
    {
        public required Guid DealId { get; init; }
        public required DateTime NewCloseDate { get; init; }
    }
}
