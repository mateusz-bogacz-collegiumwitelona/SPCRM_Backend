namespace Services.Response.Invoice
{
    public record InvoiceDetailResponse
    {
        public required Guid InvoiceId { get; init; }
        public required string InvoiceNumber { get; init; }
        public required DateTime IssueDate { get; init; }
        public required DateTime DueDate { get; init; }
        public DateTime? PaymentDate { get; init; }
        public required Guid CompanyId { get; init; }
        public required string CompanyName { get; init; }
        public required string CompanyNip { get; init; }
        public Guid? DealId { get; init; }
        public string? DealName { get; init; }

    }
}
