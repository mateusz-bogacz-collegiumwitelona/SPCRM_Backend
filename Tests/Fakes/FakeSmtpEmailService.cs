using Email.Interfaces;

namespace Tests.Fakes
{
    public class FakeSmtpEmailService : ISmtpEmailService
    {
        public record SentEmailRecord(
            string RecipientEmail,
            string Subject,
            string HtmlBody,
            byte[] AttachmentBytes,
            string Filename);

        public List<SentEmailRecord> SentEmails { get; } = new();

        public Task SendEmailWithAttachmentAsync(
            string recipientEmail,
            string subject,
            string htmlBody,
            byte[] attachmentBytes,
            string filename)
        {
            SentEmails.Add(new SentEmailRecord(recipientEmail, subject, htmlBody, attachmentBytes, filename));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string recipientEmail, string subject, string htmlBody)
        {
            return Task.CompletedTask;
        }
    }
}
