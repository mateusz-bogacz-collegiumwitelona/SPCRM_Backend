namespace Api.Request.User
{
    public record ConfirmChangeUserEmailRequest
    {
        public required Guid UserId { get; init; }
        public required string Token { get; init; }
    }
}
