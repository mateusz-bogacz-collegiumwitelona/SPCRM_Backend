namespace Domain.Comunication
{
    public record CreateUserDomain
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string UserName { get; init; }
        public required string Token { get; init; }
    }
}
