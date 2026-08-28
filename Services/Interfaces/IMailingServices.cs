using Domain.Common;
using Services.Command.Mailing;
using Services.Command.Support;

namespace Services.Interfaces
{
    public interface IMailingServices
    {
        Task<Result> SendEmailToSupport(SupportEmailCommand command);
        Task<Result> SendProductMailingAsync(MailingCommand command, Guid authorId);

    }
}
