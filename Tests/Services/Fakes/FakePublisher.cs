using MediatR;

namespace Tests.Services.Fakes
{
    public class FakePublisher : IPublisher
    {
        public List<object> PublishedEvents { get; } = new();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }
    }
}
