using Email.Interfaces;
using Infrastructure;
using Infrastructure.Pdf.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Worker.Workers
{
    public class InvoiceEmailWorker : IInvoiceEmailWorker
    {
        private readonly AppDbContext _context;
        private readonly IInvoicePdfGenerator _pdfGenerator;
        private readonly ISmtpEmailService _smtpService;
        private readonly ILogger _logger;

        public InvoiceEmailWorker(
            AppDbContext context,
            IInvoicePdfGenerator pdfGenerator,
            ISmtpEmailService smtpService,
            ILogger logger)
        {
            _context = context;
            _pdfGenerator = pdfGenerator;
            _smtpService = smtpService;
            _logger = logger;
        }

        public async Task GenerateAndSendInvoiceEmailAsync(
            Guid invoiceId,
            string recipientEmail,
            string subject,
            string htmlBody,
            string language)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Currency)
                .Include(i => i.Company)
                .Include(i => i.Deal)
                    .ThenInclude(d => d!.DealProducts)
                        .ThenInclude(dp => dp.Product)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found. Generation aborted.", invoiceId);
                return;
            }

            var lang = language?.ToLower() == "en" ? "en" : "pl";

            byte[] pdfBytes = _pdfGenerator.GenerateInvoicePdf(invoice, lang);

            var safeNumber = invoice.InvoiceNumber.Replace("/", "_").Replace("\\", "_");
            var filename = lang == "en"
                ? $"Invoice_{safeNumber}.pdf"
                : $"Faktura_{safeNumber}.pdf";

            await _smtpService.SendEmailWithAttachmentAsync(
                recipientEmail, 
                subject, 
                htmlBody, 
                pdfBytes, 
                filename);

            _logger.LogInformation("Invoice {InvoiceNumber} ({Language}) sent to {Email}.", invoice.InvoiceNumber, lang, recipientEmail);
        }
    }
}
