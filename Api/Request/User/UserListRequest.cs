using Api.Request.List;

namespace Api.Request.User
{
    public record UserListRequest : BasicListRequest
    {
        public bool? IsBlocked { get; init; }
        public string? Role { get; init; }
    }
}
