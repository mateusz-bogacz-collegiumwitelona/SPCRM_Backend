namespace DTO.Request
{
    public record CompanyFilerRequest
    {
        public bool? IsYour { get; init; }
        public DateTime? CreatedAtFrom { get; init; }
        public DateTime? CreatedAtTo { get; init; }
    }
}
