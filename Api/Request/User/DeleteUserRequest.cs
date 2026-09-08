namespace Api.Request.User
{
    public record DeleteUserRequest
    {
        public required Guid UserId { get; init; }
        public required Guid ReassignToUserId { get; init; }
    }
}
