namespace Api.Request.Product
{
    public record SearchProductAutocompleteRequest
    {
        public string? Query { get; init; }
        public int Limit { get; init; } = 20;
    }
}
