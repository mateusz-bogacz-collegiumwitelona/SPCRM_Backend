namespace Services.Response.Company
{
    public record CompanyDebtSummaryResponse
    {
        public required string CurrencyCode { get; init; }
        public required decimal TotalAmount { get; init; }
        public required int DecimalPlace { get; init; }
    }
}
