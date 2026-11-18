namespace Api.Request.List
{
    public record SimpleListRequest : PaggedRequest
    {
        public string? SearchTerm { get; init; }
    }
}
