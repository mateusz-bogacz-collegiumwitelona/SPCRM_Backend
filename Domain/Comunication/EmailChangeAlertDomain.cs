namespace Domain.Comunication
{
    public record EmailChangeAlertDomain
    {
        public required string OldEmail { get; init; }
        public required string NewEmail { get; init; }
        public required string UserName { get; init; }
    }
}
