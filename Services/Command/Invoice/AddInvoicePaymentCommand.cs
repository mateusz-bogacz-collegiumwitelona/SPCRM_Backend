namespace Services.Command.Invoice
{
    public record AddInvoicePaymentCommand
    {
        public required long Amount { get; init; } // x10000
        public required DateTime PaymentDate { get; init; }
        public string? ReferenceNumber { get; init; }
        public string? Note { get; init; }
    }
}
