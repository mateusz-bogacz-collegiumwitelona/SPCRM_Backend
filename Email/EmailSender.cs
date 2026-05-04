using DTO.Domain;
using Email.Interfaces;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly IEmailQueue _emailQueue;

        public EmailSender(ILogger<EmailSender> logger, IEmailQueue emailQueue)
        {
            _logger = logger;
            _emailQueue = emailQueue;
        }

        public async Task SendReportEmailAsync(ReportDomain report)
        {
            try
            {
                var templatePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    "report.html"
                    );

                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException($"Email template not found at path: {templatePath}");
                }

                string template = await File.ReadAllTextAsync(templatePath);

                template = template.Replace("{{Name}}", report.UserName)
                                   .Replace("{{Surname}}", report.UserSurname)
                                   .Replace("{{Email}}", report.UserEmail)
                                   .Replace("{{Time}}", report.Time)
                                   .Replace("{{Title}}", report.Title)
                                   .Replace("{{Message}}", report.Message);

                string subject = $"Nowe zgłoszenie: {report.UserName} {report.UserSurname} {report.Time}";

                _emailQueue.QueueEmail(report.SupportEmail, subject, template);
                _logger.LogInformation("Email queued to {Email}", report.SupportEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendReportEmailAsync");
            }
        }
    }
}
