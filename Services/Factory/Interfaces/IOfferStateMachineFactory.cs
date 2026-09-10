using Domain.Models;
using Domain.State;

namespace Services.Factory.Interfaces
{
    public interface IOfferStateMachineFactory
    {
        OfferStateMachine Create(Offer offer);
    }
}
