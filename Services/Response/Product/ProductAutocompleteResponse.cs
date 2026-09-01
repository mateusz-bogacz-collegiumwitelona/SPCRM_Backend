namespace Services.Response.Product
{
    public record ProductAutocompleteResponse
    {
        public Guid Id { get; init; }
        public required string Name { get; init; }
        public string SteelGrade { get; init; } = string.Empty;
        public long PricePerUnit { get; init; }
    }
}
