using Domain.Comunication;

namespace Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendReportEmailAsync(ReportDomain report);
        Task SendProductMailingAsync(MailingOfferDomain domain);
        Task SendCreateUserEmailAsync(CreateUserDomain create);
        Task SendLockoutEmailAsync(string email, DateTimeOffset lockoutEnd);
        Task SendUnlockEmailAsync(string email);
        Task SendEmailChangeConfirmationLinkAsync(EmailChangeInitiatedDomain domain);
        Task SendEmailChangeSecurityAlertAsync(EmailChangeAlertDomain domain);
    }
}
