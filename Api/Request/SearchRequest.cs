namespace Api.Request
{
    public record SearchRequest
    {
        public string? SearchTerm { get; init; }
    }
}
