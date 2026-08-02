namespace Api.Request
{
    public record SimpleListRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SearchTerm { get; init; }
    }
}
