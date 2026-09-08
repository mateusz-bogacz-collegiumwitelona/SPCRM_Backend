namespace Services.Command.Auth
{
    public record ForgotPasswordCommand
    {
        public required string Email { get; init; }
    }
}
