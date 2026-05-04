using DTO.Domain;
using Email.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Threading.Channels;

namespace Email
{
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<(string To, string Subject, string Body)> _channel;

        public EmailQueue()
        {
            var option = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait
            };

            _channel = Channel.CreateBounded<(string, string, string)>(option);
        }

        public void QueueEmail(string to, string subject, string body)
            => _channel.Writer.TryWrite((to, subject, body));

        public async ValueTask<EmailDomain> DequeueAsync(CancellationToken cancellationToken)
        {
            var (to, subject, body) = await _channel.Reader.ReadAsync(cancellationToken);
            return new EmailDomain(to, subject, body);
        }
    }
}
