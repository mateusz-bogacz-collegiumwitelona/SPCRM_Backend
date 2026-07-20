namespace Services.Command
{
    public record CompanyCommand
    {
        public Guid CompanyId { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
    }
}
