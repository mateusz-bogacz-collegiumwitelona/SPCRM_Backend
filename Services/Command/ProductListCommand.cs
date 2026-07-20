namespace Services.Command
{
    public record ProductListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public string? SearchTerm { get; init; }
        public string? ProductCategory { get; init; }
        public string? SteelGrade { get; init; }
    }
}
