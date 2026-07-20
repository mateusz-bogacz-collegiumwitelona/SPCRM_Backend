namespace Api.Request
{
    public record ContactFilterRequest
    {
        public string? ComapnyName { get; init; }
        public bool? IsPrimary { get; init; }
    }
}
