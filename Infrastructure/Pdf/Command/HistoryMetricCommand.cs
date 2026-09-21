namespace Infrastructure.Pdf.Command
{
    public record HistoryMetricCommand
    {
        public required string Label { get; init; }
        public required decimal Revenue { get; init; }
        public required int DealsWonCount { get; init; }
    }
}
