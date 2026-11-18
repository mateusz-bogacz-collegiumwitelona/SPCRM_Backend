using Services.Command.List;

namespace Services.Command.Deal
{
    public record DealListCommand : BasicListCommand
    {
        public string? CompanyName { get; init; }
        public decimal? Value { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }
        public string? StatusType { get; init; }
        public Guid? OwnerId { get; init; }
    }
}
