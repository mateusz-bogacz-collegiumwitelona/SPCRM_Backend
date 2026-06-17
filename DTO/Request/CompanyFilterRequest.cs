namespace DTO.Request
{
    public record CompanyFilterRequest
    {
        public bool? IsYour { get; init; }
        public DateTime? CreatedAtFrom { get; init; }
        public DateTime? CreatedAtTo { get; init; }
    }
}
