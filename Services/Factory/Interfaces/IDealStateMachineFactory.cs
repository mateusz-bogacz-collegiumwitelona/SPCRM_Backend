using Domain.Models;
using Domain.State;

namespace Services.Factory.Interfaces
{
    public interface IDealStateMachineFactory
    {
        DealStateMachine Create(Deal deal);
    }
}
