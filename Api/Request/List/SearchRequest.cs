namespace Api.Request.List
{
    public record SearchRequest
    {
        public string? SearchTerm { get; init; }
    }
}
