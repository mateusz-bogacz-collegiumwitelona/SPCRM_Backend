namespace Infrastructure.Pdf.Command
{
    public record EmployeeHistoryMetricCommand
    {
        public required string Label { get; init; }
        public required decimal Revenue { get; init; }
        public required int DealsWonCount { get; init; }
    }
}
