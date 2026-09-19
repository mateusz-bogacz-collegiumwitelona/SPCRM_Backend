using MediatR;

namespace Domain.Events
{
    public record OfferAcceptedEvent(Guid OfferId) : INotification;
}
