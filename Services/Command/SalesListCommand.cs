namespace Services.Command
{
    public record SalesListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
        public string? SearchTerm { get; init; }
        public string? CompanyName { get; init; }
        public decimal? Value { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }
        public string? StatusType { get; init; }
    }
}
