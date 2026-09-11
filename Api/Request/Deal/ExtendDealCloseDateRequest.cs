namespace Api.Request.Deal
{
    public record ExtendDealCloseDateRequest
    {
        public required Guid DealId { get; init; }
        public required DateTime NewCloseDate { get; init; }
    }
}
