namespace Services.Response.User
{
    public record UserDetailResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public string? PendingEmail { get; init; }
        public required bool IsEmailVerified { get; init; }
        public bool IsLocked { get; init; }
        public DateTime? LockoutEndDate { get; init; }

        public int? CompanyOwnerCount { get; init; }
        public int? ContactOwnerCount { get; init; }
        public int? ActiveDealCount { get; init; }
        public int? ActiveTaskCount { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
