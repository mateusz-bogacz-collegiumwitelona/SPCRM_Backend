namespace Api.Request.Product
{
    public record ProductFilterRequest
    {
        public string? ProductCategory { get; init; }
        public string? SteelGrade { get; init; }

        public bool? HasActivePromotion { get; init; }

    }
}
