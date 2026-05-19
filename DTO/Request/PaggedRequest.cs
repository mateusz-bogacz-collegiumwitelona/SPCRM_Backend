namespace DTO.Request
{
    public record PaggedRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }

        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;

        public string? SearchTerm { get; init; }
    }
}
