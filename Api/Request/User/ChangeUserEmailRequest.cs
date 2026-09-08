namespace Api.Request.User
{
    public record ChangeUserEmailRequest
    {
        public required Guid UserId { get; init; }
        public required string NewEmail { get; init; }
    }
}
