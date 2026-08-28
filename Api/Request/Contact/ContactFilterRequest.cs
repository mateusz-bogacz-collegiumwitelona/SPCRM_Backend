namespace Api.Request.Contact
{
    public record ContactFilterRequest
    {
        public string? ComapnyName { get; init; }
        public bool? IsPrimary { get; init; }
        public Guid? OwnerId { get; init; }
    }
}
