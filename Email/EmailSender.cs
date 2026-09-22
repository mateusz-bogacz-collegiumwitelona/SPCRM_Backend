using Domain.Comunication;
using Domain.Constants;
using Email.Interfaces;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Email
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IConfiguration _config;
        private readonly string _host;

        public EmailSender(
            ILogger<EmailSender> logger,
            IBackgroundJobClient backgroundJobClient,
            IConfiguration config)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _config = config;
            _host = _config["Frontend:Url"] ?? "http://localhost:5173";
        }

        public async Task SendReportEmailAsync(ReportDomain report)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync(
                    "report.html",
                    "Zgłoszenie techniczne / Błąd",
                    "pl",
                    "background-color: #0f172a;"); 

                template = template
                    .Replace("{{Name}}", report.UserName)
                    .Replace("{{Surname}}", report.UserSurname)
                    .Replace("{{Email}}", report.UserEmail)
                    .Replace("{{Time}}", report.Time)
                    .Replace("{{Title}}", report.Title)
                    .Replace("{{Message}}", report.Message);

                string subject = $"Nowe zgłoszenie: {report.UserName} {report.UserSurname} {report.Time}";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(report.SupportEmail, subject, template));
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
                string language = domain.Language.ToLower() == "en" ? "en" : "pl";
                string title = language == "en" ? "Steel Products Quotation" : "Oferta wyrobów hutniczych";
                string subject = language == "en" ? "New Product Offer" : "Nowa oferta produktów";

                var template = await GetTemplateWithLayoutAsync(
                    Path.Combine("Product-Offert", $"product-offert-{language}.html"),
                    title,
                    language
                );

                var sb = new System.Text.StringBuilder();

                foreach (var p in domain.Products)
                {
                    decimal actualWeight = p.Weight / BusinessConstants.WeightScaleFactor;
                    decimal formattedFinalPrice = p.FinalPrice / BusinessConstants.CurrencyScaleFactor;

                    string nameCell = p.ProductName;
                    if (p.IsPromoted)
                    {
                        nameCell += " <span style='color: white; background-color: #e74c3c; padding: 2px 6px; font-size: 11px; font-weight: bold; border-radius: 4px; margin-left: 5px;'>HIT</span>";
                    }

                    string priceCell = $"{formattedFinalPrice:0.00} {p.CurrencyCode}";
                    if (p.OriginalPrice.HasValue)
                    {
                        decimal formattedOriginalPrice = p.OriginalPrice.Value / BusinessConstants.CurrencyScaleFactor;

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
                    _backgroundJobClient.Enqueue<ISmtpEmailService>(
                        x => x.SendEmailAsync(email, subject, finalizedHtmlTemplate));
                }

                _logger.LogInformation("{Count} offer emails have been successfully queued.", domain.BccEmails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendProductMailingAsync");
            }
        }

        public async Task SendCreateUserEmailAsync(CreateUserDomain create)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync("create-user.html", "Witaj w systemie SPCRM", "pl");

                string encodeToken = Uri.EscapeDataString(create.Token);
                string encodedEmail = Uri.EscapeDataString(create.Email);
                string link = $"{_host}/auth/confirm-registration?token={encodeToken}&email={encodedEmail}";

                template = template
                    .Replace("{{FirstName}}", create.FirstName)
                    .Replace("{{LastName}}", create.LastName)
                    .Replace("{{Email}}", create.Email)
                    .Replace("{{UserName}}", create.UserName)
                    .Replace("{{Link}}", link);

                string subject = "Witamy w SPCRM";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(create.Email, subject, template));
                _logger.LogInformation("Create user email queued to {Email}", create.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendCreateUserEmailAsync");
            }
        }

        public async Task SendLockoutEmailAsync(string email, DateTimeOffset lockoutEnd)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync(
                    "lockout.html",
                    "Konto zostało zablokowane",
                    "pl",
                    "background-color: #b91c1c;");

                string lockoutMsg = lockoutEnd == DateTimeOffset.MaxValue
                    ? "<p style='margin:0;'>Twoje konto zostało zablokowane na stałe.</p>"
                    : $"<p style='margin:0;'>Twoje konto zostało zablokowane do {lockoutEnd.LocalDateTime:dd.MM.yyyy HH:mm}.</p>";

                template = template.Replace("{{LockoutMessage}}", lockoutMsg);

                string subject = "Informacja o blokadzie konta";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(email, subject, template));
                _logger.LogInformation("Lockout email queued to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendLockoutEmailAsync");
            }
        }

        public async Task SendUnlockEmailAsync(string email)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync(
                    "unlock.html",
                    "Konto zostało odblokowane",
                    "pl",
                    "background-color: #15803d;");

                string subject = "Informacja o odblokowaniu konta";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(email, subject, template));
                _logger.LogInformation("Unlock email queued to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendUnlockEmailAsync");
            }
        }

        public async Task SendEmailChangeConfirmationLinkAsync(EmailChangeInitiatedDomain domain)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync("email-change-confirmation.html", "Potwierdzenie nowego adresu e-mail", "pl");

                string encodedUserId = Uri.EscapeDataString(domain.UserId.ToString());
                string link = $"{_host}/auth/confirm-email-change?userId={encodedUserId}&token={domain.Token}";

                template = template
                    .Replace("{{UserName}}", domain.UserName)
                    .Replace("{{Link}}", link);

                string subject = "Potwierdzenie zmiany adresu e-mail w systemie SPCRM";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(domain.NewEmail, subject, template));
                _logger.LogInformation("Email change confirmation link queued to new address: {NewEmail}", domain.NewEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendEmailChangeConfirmationLinkAsync for user {UserId}", domain.UserId);
            }
        }

        public async Task SendEmailChangeSecurityAlertAsync(EmailChangeAlertDomain domain)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync(
                    "email-change-alert.html",
                    "Zgłoszono zmianę adresu e-mail",
                    "pl",
                    "background-color: #b45309;");

                template = template
                    .Replace("{{UserName}}", domain.UserName)
                    .Replace("{{NewEmail}}", domain.NewEmail);

                string subject = "Alert bezpieczeństwa: Zgłoszenie zmiany adresu e-mail w SPCRM";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(domain.OldEmail, subject, template));
                _logger.LogInformation("Email change security alert queued to old address: {OldEmail}", domain.OldEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendEmailChangeSecurityAlertAsync for user {UserName}", domain.UserName);
            }
        }

        public async Task SendPasswordResetEmailAsync(ResetPasswordEmailDomain domain)
        {
            try
            {
                var template = await GetTemplateWithLayoutAsync("reset-password.html", "Resetowanie hasła", "pl");

                string encodedUserId = Uri.EscapeDataString(domain.UserId.ToString());
                string link = $"{_host}/auth/reset-password?userId={encodedUserId}&token={domain.Token}";

                template = template
                    .Replace("{{UserName}}", domain.UserName)
                    .Replace("{{Link}}", link)
                    .Replace("{{Token}}", domain.Token);

                string subject = "Resetowanie hasła w systemie SPCRM";
                _backgroundJobClient.Enqueue<ISmtpEmailService>(x => x.SendEmailAsync(domain.Email, subject, template));
                _logger.LogInformation("Password reset email queued to: {Email}", domain.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendPasswordResetEmailAsync for user {UserId}", domain.UserId);
            }
        }

        public async Task SendInvoiceEmailAsync(InvoiceEmailDomain domain)
        {
            try
            {
                string language = domain.Language?.ToLower() == "en" ? "en" : "pl";
                string title = language == "en" ? $"VAT Invoice {domain.InvoiceNumber}" : $"Faktura VAT nr {domain.InvoiceNumber}";
                string subject = title;

                var template = await GetTemplateWithLayoutAsync(
                    Path.Combine("Invoice", $"invoice-mail-{language}.html"),
                    title,
                    language
                );

                template = template
                    .Replace("{{RecipientName}}", domain.RecipientName)
                    .Replace("{{InvoiceNumber}}", domain.InvoiceNumber)
                    .Replace("{{TotalAmount}}", domain.TotalGrossAmount.ToString("F2"))
                    .Replace("{{Currency}}", domain.CurrencyCode)
                    .Replace("{{DueDate}}", domain.DueDate.ToString("yyyy-MM-dd"));

                _backgroundJobClient.Enqueue<IInvoiceEmailWorker>(x =>
                    x.GenerateAndSendInvoiceEmailAsync(domain.InvoiceId, domain.RecipientEmail, subject, template, language));

                _logger.LogInformation("Invoice email queued to Hangfire ({Language}) for recipient {Email}", language, domain.RecipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queueing invoice email for InvoiceId: {InvoiceId}", domain.InvoiceId);
            }
        }

        private async Task<string> GetTemplateWithLayoutAsync(
          string relativePath,
          string title,
          string language = "pl",
          string? headerStyle = null)
        {
            var templatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            var layoutPath = Path.Combine(templatesDir, "_layout.html");
            var templatePath = Path.Combine(templatesDir, relativePath);

            if (!File.Exists(layoutPath))
                throw new FileNotFoundException($"Layout template not found at: {layoutPath}");

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Email template not found at: {templatePath}");

            var layoutHtml = await File.ReadAllTextAsync(layoutPath);
            var contentHtml = await File.ReadAllTextAsync(templatePath);

            var lang = language.ToLower() == "en" ? "en" : "pl";
            var footerText = lang == "en"
                ? "This message was generated automatically by SPCRM. Please do not reply."
                : "Wiadomość wygenerowana automatycznie przez system SPCRM. Prosimy na nią nie odpowiadać.";

            return layoutHtml
                .Replace("{{Lang}}", lang)
                .Replace("{{Title}}", title)
                .Replace("{{HeaderStyle}}", headerStyle ?? "background-color: #004a8f;")
                .Replace("{{FooterText}}", footerText)
                .Replace("{{Content}}", contentHtml);
        }
    }
}
