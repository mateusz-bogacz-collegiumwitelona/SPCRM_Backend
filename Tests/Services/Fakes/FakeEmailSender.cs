using Domain.Comunication;
using Services.Interfaces;

namespace Tests.Services.Fakes
{
    public class FakeEmailSender : IEmailSender
    {
        public List<CreateUserDomain> SentCreateUserEmails { get; } = new();
        public List<ReportDomain> SentReportEmails { get; } = new();
        public List<MailingOfferDomain> SentProductMailingEmails { get; } = new();
        public List<(string Email, DateTimeOffset LockoutEnd)> SentLockoutEmails { get; } = new();
        public List<string> SentUnlockEmails { get; } = new();

        public List<ResetPasswordEmailDomain> SentResetPasswordEmails { get; } = new();

        public List<EmailChangeInitiatedDomain> SentEmailChangeConfirmationLinks { get; } = new();
        public List<EmailChangeAlertDomain> SentEmailChangeSecurityAlerts { get; } = new();
        public List<InvoiceEmailDomain> SentInvoiceEmails { get; } = new();

        public ReportDomain? SentReport => SentReportEmails.LastOrDefault();
        public MailingOfferDomain? LastSentProductMailing => SentProductMailingEmails.LastOrDefault();
        public EmailChangeInitiatedDomain? LastSentConfirmationLink => SentEmailChangeConfirmationLinks.LastOrDefault();
        public EmailChangeAlertDomain? LastSentSecurityAlert => SentEmailChangeSecurityAlerts.LastOrDefault();

        public InvoiceEmailDomain? LasInvoiceEmailDomain => SentInvoiceEmails.LastOrDefault();

        public int CallCount => SentCreateUserEmails.Count +
                                SentReportEmails.Count +
                                SentProductMailingEmails.Count +
                                SentLockoutEmails.Count +
                                SentUnlockEmails.Count +
                                SentEmailChangeConfirmationLinks.Count +
                                SentEmailChangeSecurityAlerts.Count;

        public bool EmailSent => CallCount > 0;

        public Task SendCreateUserEmailAsync(CreateUserDomain create)
        {
            SentCreateUserEmails.Add(create);
            return Task.CompletedTask;
        }

        public Task SendReportEmailAsync(ReportDomain report)
        {
            SentReportEmails.Add(report);
            return Task.CompletedTask;
        }

        public Task SendProductMailingAsync(MailingOfferDomain domain)
        {
            SentProductMailingEmails.Add(domain);
            return Task.CompletedTask;
        }

        public Task SendLockoutEmailAsync(string email, DateTimeOffset lockoutEnd)
        {
            SentLockoutEmails.Add((email, lockoutEnd));
            return Task.CompletedTask;
        }

        public Task SendUnlockEmailAsync(string email)
        {
            SentUnlockEmails.Add(email);
            return Task.CompletedTask;
        }

        public Task SendEmailChangeConfirmationLinkAsync(EmailChangeInitiatedDomain domain)
        {
            SentEmailChangeConfirmationLinks.Add(domain);
            return Task.CompletedTask;
        }

        public Task SendEmailChangeSecurityAlertAsync(EmailChangeAlertDomain domain)
        {
            SentEmailChangeSecurityAlerts.Add(domain);
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(ResetPasswordEmailDomain domain)
        {
            SentResetPasswordEmails.Add(domain);
            return Task.CompletedTask;
        }

        public Task SendInvoiceEmailAsync(InvoiceEmailDomain domain)
        {
            SentInvoiceEmails.Add(domain);
            return Task.CompletedTask;
        }
    }
}
