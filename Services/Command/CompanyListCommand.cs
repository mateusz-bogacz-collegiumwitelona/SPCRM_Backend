namespace Services.Command
{
    public record CompanyListCommand
    {
        public required Guid UserId { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public bool? IsYour { get; init; }
        public DateTime? CreatedAtFrom { get; init; }
        public DateTime? CreatedAtTo { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public string? SearchTerm { get; init; }
    }
}
