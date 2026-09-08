namespace Api.Request.Auth
{
    public record ForgotPasswordRequest
    {
        public required string Email { get; init; }
    }
}
