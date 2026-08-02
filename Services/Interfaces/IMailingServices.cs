using Domain.Common;
using Services.Command;

namespace Services.Interfaces
{
    public interface IMailingServices
    {
        Task<Result> SendEmailToSupport(SupportEmailCommand command);
        Task<Result> SendProductMailingAsync(MailingCommand command); 

    }
}
