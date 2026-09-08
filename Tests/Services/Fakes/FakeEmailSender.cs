using Domain.Comunication;
using Services.Interfaces;

namespace Tests.Services.Fakes
{
    public class FakeEmailSender : IEmailSender
    {
        public List<CreateUserDomain> SentCreateUserEmails { get; } = new();
        public List<ReportDomain> SentReportEmails { get; } = new();
        public List<MailingOfferDomain> SentProductMailingEmails { get; } = new();

        public ReportDomain? SentReport => SentReportEmails.LastOrDefault();
        public MailingOfferDomain? LastSentProductMailing => SentProductMailingEmails.LastOrDefault();
        public int CallCount => SentCreateUserEmails.Count + SentReportEmails.Count + SentProductMailingEmails.Count;
        public bool EmailSent => CallCount > 0;
        public List<(string Email, DateTimeOffset LockoutEnd)> SentLockoutEmails { get; } = new();

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
    }
}
