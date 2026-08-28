namespace Api.Request.List
{
    public record PaggedRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
    }
}
