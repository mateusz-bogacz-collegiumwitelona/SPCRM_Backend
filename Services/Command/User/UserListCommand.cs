namespace Services.Command.User
{
    public record UserListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public bool? IsBlocked { get; init; }
        public string? Role { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public string? SearchTerm { get; init; }
    }
}
