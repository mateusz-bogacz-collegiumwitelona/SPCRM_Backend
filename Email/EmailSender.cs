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
    
                    decimal formattedFinalPrice = p.FinalPrice / 10000m;

                    string nameCell = p.ProductName;
                    if (p.IsPromoted)
                    {
                        nameCell += " <span style='color: white; background-color: #e74c3c; padding: 2px 6px; font-size: 11px; font-weight: bold; border-radius: 4px; margin-left: 5px;'>HIT</span>";
                    }

                    string priceCell = $"{formattedFinalPrice:0.00} {p.CurrencyCode}";
                    if (p.OriginalPrice.HasValue)
                    {
                        decimal formattedOriginalPrice = p.OriginalPrice.Value / 10000m;

                        priceCell = $"<s style='color: #7f8c8d; font-size: 12px;'>{formattedOriginalPrice:0.00}</s><br/>"
                                  + $"<strong style='color: #27ae60; font-size: 14px;'>{formattedFinalPrice:0.00} {p.CurrencyCode}</strong>";

                        if (p.DiscountPercentage.HasValue)
                        {
                            priceCell += $"<br/><span style='font-size: 11px; color: #e74c3c; font-weight: bold;'>-{p.DiscountPercentage:0.##}%</span>";
                        }
                    }

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{nameCell}</td>");
                    sb.AppendLine($"<td>{p.SteelGrade}</td>");
                    sb.AppendLine($"<td>{p.FormattedDimensions}</td>");
                    sb.AppendLine($"<td>{actualWeight:0.##} kg</td>");
                    sb.AppendLine($"<td>{p.Quantity} {p.UnitSymbol}</td>");
                    sb.AppendLine($"<td>{priceCell}</td>");
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
