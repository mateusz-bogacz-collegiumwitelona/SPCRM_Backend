namespace Services.Command
{
    public record ContactListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? ComapnyName { get; init; }
        public bool? IsPrimary { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public string? SearchTerm { get; init; }
    }
}
