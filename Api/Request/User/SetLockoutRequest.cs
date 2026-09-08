namespace Api.Request.User
{
    public record SetLockoutRequest
    {
        public required Guid UserId { get; init; }
        public DateTime? LockoutEnd { get; init; }
    }
}
