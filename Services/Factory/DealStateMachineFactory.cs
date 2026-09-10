using Domain.Models;
using Domain.State;
using Services.Factory.Interfaces;

namespace Services.Factory
{
    public class DealStateMachineFactory : IDealStateMachineFactory
    {
        public DealStateMachine Create(Deal deal) => new DealStateMachine(deal);
    }
}
