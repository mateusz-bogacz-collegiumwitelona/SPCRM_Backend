using Domain.Models;
using Domain.State;
using Services.Factory.Interfaces;

namespace Services.Factory
{
    public class OfferStateMachineFactory : IOfferStateMachineFactory
    {
        public OfferStateMachine Create(Offer offer) => new OfferStateMachine(offer);
    }
}
