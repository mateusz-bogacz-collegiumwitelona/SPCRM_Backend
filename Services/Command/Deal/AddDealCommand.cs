namespace Services.Command.Deal
{
    public record AddDealCommand
    {
        public required DateTime CloseDate { get; init; }
        public required Guid CurrencyId { get; init; }
        public required Guid CompanyId { get; init; }
        public required List<AddDealProductCommand> Products { get; init; } = new();
    }
}
