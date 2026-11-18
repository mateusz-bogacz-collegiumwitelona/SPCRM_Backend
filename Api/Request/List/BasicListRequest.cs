namespace Api.Request.List
{
    public record BasicListRequest : SimpleListRequest
    {
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
    }
}
