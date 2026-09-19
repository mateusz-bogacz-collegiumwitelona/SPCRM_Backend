namespace Services.Response.Deal
{
    public record DealAssignableContactResponse
    {
        public required Guid Id { get; init; }
        public required string FullName { get; init; }
        public string? JobTitle { get; init; }
        public required string Email { get; init; }
        public required bool IsPrimary { get; init; }
    }
}
