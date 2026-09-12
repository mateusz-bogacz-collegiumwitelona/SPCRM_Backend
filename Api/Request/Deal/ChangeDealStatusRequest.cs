namespace Api.Request.Deal
{
    public class ChangeDealStatusRequest
    {
        public required string TargetStatus { get; init; }
        public string? Language { get; init; }
        public string? CustomRecipientEmail { get; init; }
    }
}
