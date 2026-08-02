using Domain.Comunication;

namespace Email.Interfaces
{
    public interface IEmailQueue
    {
        void QueueEmail(string to, string subject, string body);
        ValueTask<EmailDomain> DequeueAsync(CancellationToken cancellationToken);
        ValueTask QueueEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    }
}
