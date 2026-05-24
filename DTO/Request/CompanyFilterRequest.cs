namespace DTO.Request
{
    public record CompanyFilterRequest
    {
        public string? CompanyName { get; init; }
        public decimal? Value { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }

        public string? StatusType { get; init; }
    }
}
