using DTO.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendReportEmailAsync(ReportDomain report);
    }
}
