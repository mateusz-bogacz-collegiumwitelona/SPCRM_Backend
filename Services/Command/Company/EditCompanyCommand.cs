namespace Services.Command.Company
{
    public record EditCompanyCommand
    {
        public required Guid Id { get; init; }
        public string? Name { get; init; } 
        public string? NIP { get; init; }
    }
}
