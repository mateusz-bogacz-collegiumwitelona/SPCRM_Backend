namespace DTO.Response
{
    public record ContactWayResponse
    {
        public required string Type { get; init; }

        public required string Value { get; init; }

        public string? Label { get; init; }

        public bool IsPrimary { get; init; }
    }
}
