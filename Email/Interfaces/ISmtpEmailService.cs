namespace Email.Interfaces
{
    public interface ISmtpEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendEmailWithAttachmentAsync(
            string to,
            string subject,
            string body,
            byte[] attachmentBytes,
            string attachmentFilename);
    }
}
