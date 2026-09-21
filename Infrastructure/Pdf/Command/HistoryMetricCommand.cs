namespace Infrastructure.Pdf.Command
{
    public record HistoryMetricCommand
    {
        public required string Label { get; init; }
        public List<CurrencyAmountCommand> Revenue { get; init; } = new();
        public required int DealsWonCount { get; init; }
    }
}
