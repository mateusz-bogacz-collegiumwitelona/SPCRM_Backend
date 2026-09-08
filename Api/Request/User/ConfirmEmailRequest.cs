namespace Api.Request.User
{
    public record ConfirmEmailRequest
    {
        public required string Email { get; init; }
        public required string Token { get; init; }
    }
}
