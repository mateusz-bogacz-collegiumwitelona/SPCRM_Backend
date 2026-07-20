using Domain.Comunication;

namespace Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendReportEmailAsync(ReportDomain report);
    }
}
