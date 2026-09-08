namespace Api.Request.User
{
    public record AddUserRequest
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Role { get; init; }
        public required string Password { get; init; }
        public required string ConfirmPassword { get; init; }

    }
}
