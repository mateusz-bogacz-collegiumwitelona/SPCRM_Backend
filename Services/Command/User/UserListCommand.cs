using Services.Command.List;

namespace Services.Command.User
{
    public record UserListCommand : BasicListCommand
    {
        public bool? IsBlocked { get; init; }
        public string? Role { get; init; }
    }
}
