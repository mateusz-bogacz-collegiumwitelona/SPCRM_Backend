namespace Domain.Comunication
{
    public class InvoiceEmailDomain
    {
        public required Guid InvoiceId { get; init; }
        public required string RecipientEmail { get; init; }
        public required string RecipientName { get; init; }
        public required string InvoiceNumber { get; init; }
        public required decimal TotalGrossAmount { get; init; }
        public required string CurrencyCode { get; init; }
        public required DateTime DueDate { get; init; }
        public string Language { get; init; } = "pl";
    }
}
