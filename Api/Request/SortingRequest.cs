namespace Api.Request
{
    public record SortingRequest
    {
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = false;
    }
}
