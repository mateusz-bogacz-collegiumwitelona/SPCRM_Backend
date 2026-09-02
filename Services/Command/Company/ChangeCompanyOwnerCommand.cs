namespace Services.Command.Company
{
    public record ChangeCompanyOwnerCommand
    {
        public required Guid CompanyId { get; init; }
        public required Guid UserId { get; init; }
    }
}
