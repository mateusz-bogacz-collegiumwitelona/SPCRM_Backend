namespace DTO.Request
{
    public record DealProductFilterRequest
    {
        public string? ProductCategory { get; init; }
        public string? SteelGrade { get; init; }

    }
}
