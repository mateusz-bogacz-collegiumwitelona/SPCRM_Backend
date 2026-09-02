namespace Services.Command.Company
{
    public record AddCompanyCommand
    {
        public required string Name { get; init; }
        public required string NIP { get; init; }
        public List<AddCompanyAdressCommand> Adresses { get; init; } = new();
    }
}
