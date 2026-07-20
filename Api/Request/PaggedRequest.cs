namespace Api.Request
{
    public record PaggedRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
    }
}
