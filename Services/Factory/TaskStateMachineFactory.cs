using Domain.Models;
using Domain.State;
using Services.Factory.Interfaces;

namespace Services.Factory
{
    public class TaskStateMachineFactory : ITaskStateMachineFactory
    {
        public TaskStateMachine Create(Tasks task) => new TaskStateMachine(task);
    }
}
