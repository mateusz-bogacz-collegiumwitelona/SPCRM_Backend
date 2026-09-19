namespace Domain.Comunication
{
    public record EmailChangeInitiatedDomain
    {
        public required string OldEmail { get; init; }
        public required string NewEmail { get; init; }
        public required string UserName { get; init; }
        public required string Token { get; init; }
        public required Guid UserId { get; init; }
    }
}
