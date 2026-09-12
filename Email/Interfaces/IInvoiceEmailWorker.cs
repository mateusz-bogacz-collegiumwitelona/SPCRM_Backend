namespace Email.Interfaces
{
    public interface IInvoiceEmailWorker
    {
        Task GenerateAndSendInvoiceEmailAsync(
            Guid invoiceId,
            string recipientEmail,
            string subject,
            string htmlBody,
            string language);
    }
}
