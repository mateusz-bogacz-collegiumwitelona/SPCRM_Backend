namespace Api.Request.Auth
{
    public record ResetPasswordRequest
    {
        public required Guid UserId { get; init; }
        public required string Token { get; init; }
        public required string Password { get; init; }
        public required string ConfirmPassword { get; init; }
    }
}
