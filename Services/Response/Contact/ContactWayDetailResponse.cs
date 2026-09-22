namespace Services.Response.Contact
{
    public record ContactWayDetailResponse
    {
        public required Guid ContactDetailId { get; set; }
        public required string Label { get; init; }
        public required string Value { get; init; }
        public bool IsPrimary { get; set; }
        public required string Type { get; set; }
    }
}
