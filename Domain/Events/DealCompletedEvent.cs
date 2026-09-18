using MediatR;

namespace Domain.Events
{
    public record DealCompletedEvent(Guid DealId,
        string? RecipientEmail,
        string? Language
    ) : INotification;
}
