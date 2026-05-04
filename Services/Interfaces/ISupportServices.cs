using Domain.Common;
using DTO.Request;

namespace Services.Interfaces
{
    public interface ISupportServices
    {
        Task<Result> SendEmailToSupport(SupportEmailRequest request);
    }
}
