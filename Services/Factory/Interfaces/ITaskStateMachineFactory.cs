using Domain.Models;
using Domain.State;

namespace Services.Factory.Interfaces
{
    public interface ITaskStateMachineFactory
    {
        TaskStateMachine Create(Tasks task);
    }
}
