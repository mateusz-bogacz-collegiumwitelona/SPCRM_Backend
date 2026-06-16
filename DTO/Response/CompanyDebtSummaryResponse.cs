namespace DTO.Response
{
    public record CompanyDebtSummaryResponse
    {
        public required string CurrencyCode { get; init; }
        public required decimal TotalAmount { get; init; }
        public required int DecimalPlace { get; init; }
    }
}
