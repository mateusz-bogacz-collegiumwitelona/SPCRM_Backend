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
            var host = _config["Mail:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["Mail:Port"] ?? "587");
            var enableSsl = bool.Parse(_config["Mail:EnableSsl"] ?? "true");
            var fromEmail = _config["Mail:From"];
            var displayName = _config["Mail:DisplayName"] ?? "SPCRM System";
            var password = _config["Mail:Password"];

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


            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(fromEmail, displayName);
            mailMessage.To.Add(new MailAddress(to));
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(host, port);
            smtpClient.EnableSsl = enableSsl;
            smtpClient.UseDefaultCredentials = false;

            if (!string.IsNullOrEmpty(password))
            {
                smtpClient.Credentials = new NetworkCredential(fromEmail, password);
            }

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Email SENT via Hangfire to {Email} | Subject: {Subject}", to, subject);
        }
    }
}
