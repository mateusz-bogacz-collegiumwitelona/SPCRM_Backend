using Domain.Enum;

namespace Services.Command.Deal
{
    public class ChangeDealStatusCommand
    {
        public required Guid DealId { get; init; }
        public required Guid UserId { get; init; }
        public required DealsStatusEnum TargetStatus { get; init; }
        public string? Language { get; init; }
        public string? CustomRecipientEmail { get; init; }
    }
}
