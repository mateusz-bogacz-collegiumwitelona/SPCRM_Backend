namespace Api.Request.Company
{
    public class ChangeCompanyOwnerRequest
    {
        public required Guid CompanyId { get; init; }
        public required Guid UserId { get; init; }
    }
}
