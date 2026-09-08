namespace Api.Request.User
{
    public record ChangeRoleRequest
    {
        public required Guid UserId { get; init; }
        public required string Role { get; init; }
    }
}
