using DTO.Domain;

namespace Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendReportEmailAsync(ReportDomain report);
    }
}
