namespace DTO.Request
{
    public record LoginRequest
    {
        public required string Name { get; init; }
        public required string Password { get; init; }
    }
}
