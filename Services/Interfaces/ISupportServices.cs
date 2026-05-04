using Domain.Common;
using DTO.Request;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface ISupportServices
    {
        Task<Result> SendEmailToSupport(SupportEmailRequest request);
    }
}
