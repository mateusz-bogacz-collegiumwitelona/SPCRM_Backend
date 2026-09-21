using Infrastructure.Pdf.Command;

namespace Infrastructure.Pdf.Interfaces
{
    public interface IAnalyticsPdfGenerator
    {
        byte[] GenerateEmployeeReportPdf(EmployeeAnalyticsReportCommand data);
        byte[] GenerateTeamReportPdf(TeamAnalyticsReportCommand data);
    }
}
