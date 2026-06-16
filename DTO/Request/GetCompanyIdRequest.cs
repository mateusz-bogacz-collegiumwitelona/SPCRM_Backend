namespace DTO.Request
{
    public record GetCompanyIdRequest
    {
        public Guid CompanyId { get; init; }
    }
}
