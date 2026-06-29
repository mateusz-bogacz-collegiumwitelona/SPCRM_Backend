namespace DTO.Request
{
    public record ProductFilterRequest
    {
        public string? ProductCategory { get; init; }
        public string? SteelGrade { get; init; }

    }
}
