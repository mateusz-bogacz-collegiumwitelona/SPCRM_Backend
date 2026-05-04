using DTO.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Interfaces
{
    public interface IEmailQueue
    {
        void QueueEmail(string to, string subject, string body);
        ValueTask<EmailDomain> DequeueAsync(CancellationToken cancellationToken);
    }
}
