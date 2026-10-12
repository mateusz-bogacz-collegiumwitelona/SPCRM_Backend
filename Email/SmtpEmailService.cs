using Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Email
{
    public class SmtpEmailService : ISmtpEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            await SendEmailInternalAsync(to, subject, body);
        }

        public async Task SendEmailWithAttachmentAsync(
            string to,
            string subject,
            string body,
            byte[] attachmentBytes,
            string attachmentFilename)
        {
            await SendEmailInternalAsync(to, subject, body, attachmentBytes, attachmentFilename);
        }

        private async Task SendEmailInternalAsync(
            string to,
            string subject,
            string body,
            byte[]? attachmentBytes = null,
            string? attachmentFilename = null)
        {
            var fromEmail = _config["Mail:From"];
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError("An email cannot be sent. The (Mail:From) address is missing from the configuration.");
                return;
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                _logger.LogWarning("Cannot send email. The recipient 'to' address is empty. Subject: {Subject}", subject);
                return;
            }

            var displayName = _config["Mail:DisplayName"] ?? "SPCRM System";

            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(fromEmail, displayName);
            mailMessage.To.Add(new MailAddress(to));
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = true;

            MemoryStream? stream = null;
            if (attachmentBytes != null && !string.IsNullOrEmpty(attachmentFilename))
            {
                stream = new MemoryStream(attachmentBytes);
                mailMessage.Attachments.Add(new Attachment(stream, attachmentFilename, "application/pdf"));
            }

            try
            {
                using var smtpClient = CreateSmtpClient(fromEmail);
                await smtpClient.SendMailAsync(mailMessage);

                if (attachmentBytes != null)
                {
                    _logger.LogInformation("Invoice email with PDF sent to {Email}", to);
                }
                else
                {
                    _logger.LogInformation("Email SENT via Hangfire to {Email} | Subject: {Subject}", to, subject);
                }
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private SmtpClient CreateSmtpClient(string fromEmail)
        {
            var host = _config["Mail:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["Mail:Port"] ?? "587");
            var enableSsl = bool.Parse(_config["Mail:EnableSsl"] ?? "true");
            var password = _config["Mail:Password"];

            var smtpClient = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrEmpty(password))
            {
                smtpClient.Credentials = new NetworkCredential(fromEmail, password);
            }

            return smtpClient;
        }
    }
}
