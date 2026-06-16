namespace DTO.Response
{
    public record CompanyDebtDetailResponse
    {
        public required Guid Id { get; init; }
        public required string InvoiceNumber { get; init; }
        public required decimal AmountLeft { get; init; }
        public required string CurrencyCode { get; init; }
        public int DecimalPlaces { get; init; }
        public DateTime DueDate { get; init; }
        public int DaysOverdue { get; init; }
    }
}
