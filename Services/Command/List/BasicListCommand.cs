namespace Services.Command.List
{
    public record BasicListCommand : SimpleListCommand
    {
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
    }
}
