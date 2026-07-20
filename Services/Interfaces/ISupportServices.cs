using Domain.Common;
using Services.Command;

namespace Services.Interfaces
{
    public interface ISupportServices
    {
        Task<Result> SendEmailToSupport(SupportEmailCommand command);
    }
}
