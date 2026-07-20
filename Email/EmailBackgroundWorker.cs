using Domain.Comunication;
using Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Email
{
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IEmailQueue _queue;
        private readonly ILogger<EmailBackgroundWorker> _logger;
        private readonly IConfiguration _config;

        public EmailBackgroundWorker(
            IEmailQueue queue,
            ILogger<EmailBackgroundWorker> logger,
            IConfiguration config
            )
        {
            _queue = queue;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email background worker started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var email = await _queue.DequeueAsync(stoppingToken);
                    await SendSmtpEmailAsync(email);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing email.");
                }
            }
            _logger.LogInformation("Email background worker stopped.");
        }

        private async Task SendSmtpEmailAsync(EmailDomain message)
        {
            try
            {
                var host = _config["Mail:Host"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Mail:Port"] ?? "587");
                var enableSsl = bool.Parse(_config["Mail:EnableSsl"] ?? "true");
                var fromEmail = _config["Mail:From"];
                var displayName = _config["Mail:DisplayName"] ?? "SPCRM System";
                var password = _config["Mail:Password"];

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail))
                {
                    _logger.LogError("Email configuration missing. Cannot send email to {To}", message.To);
                    return;
                }

                using var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(fromEmail, displayName);
                mailMessage.To.Add(new MailAddress(message.To));
                mailMessage.Subject = message.Subject;

                mailMessage.Body = message.Body;
                mailMessage.IsBodyHtml = true;

                using var smtpClient = new SmtpClient(host, port);
                smtpClient.EnableSsl = enableSsl;
                smtpClient.UseDefaultCredentials = false;

                if (!string.IsNullOrEmpty(password))
                {
                    smtpClient.Credentials = new NetworkCredential(fromEmail, password);
                }

                smtpClient.Timeout = 10000;

                await smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email SENT (Background) to {Email} | Subject: {Subject}", message.To, message.Subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP Error sending email to {Email}", message.To);
            }
        }
    }
}
