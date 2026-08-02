using Domain.Comunication;
using Email.Interfaces;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

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

        public async Task SendProductMailingAsync(MailingOfferDomain domain)
        {
            try
            {
                string language = domain.Language.ToLower();
                var templatePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    "Product-Offert",
                    $"product-offert-{language}.html"
                    );

                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException($"Email template not found at path: {templatePath}");
                }

                string template = await File.ReadAllTextAsync(templatePath);
                string subject = language == "pl" ? $"Nowa oferta produktów" : $"New Product Offer";

                var sb = new System.Text.StringBuilder();

                foreach (var p in domain.Products)
                {
                    decimal actualWeight = p.Weight / 1000m;
                    decimal formattedPrice = p.Price / 10000m;

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{p.ProductName}</td>");
                    sb.AppendLine($"<td>{p.SteelGrade}</td>");
                    sb.AppendLine($"<td>{p.FormattedDimensions}</td>");
                    sb.AppendLine($"<td>{actualWeight:0.##} kg</td>");
                    sb.AppendLine($"<td>{p.Quantity} {p.UnitSymbol}</td>");
                    sb.AppendLine($"<td>{formattedPrice:0.00} {p.CurrencyCode}</td>");
                    sb.AppendLine("</tr>");
                }

                string finalizedHtmlTemplate = template.Replace("{{ProductRows}}", sb.ToString());

                foreach (var email in domain.BccEmails)
                {
                    await _emailQueue.QueueEmailAsync(email, subject, finalizedHtmlTemplate);
                }

                _logger.LogInformation("{Count} offer emails have been successfully queued.", domain.BccEmails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendProductMailingAsync");
            }
        }
    }
}
