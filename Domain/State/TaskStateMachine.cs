using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Stateless;

namespace Domain.State
{
    public class TaskStateMachine
    {
        private readonly Tasks _task;
        private readonly StateMachine<TaskStatusEnum, TaskTriggerEnum> _machine;

        public TaskStateMachine(Tasks task)
        {
            _task = task;
            _machine = new StateMachine<TaskStatusEnum, TaskTriggerEnum>(
                () => _task.Status,
                newStatus => _task.Status = newStatus
            );
            Configure();
        }

        private void Configure()
        {
            _machine.Configure(TaskStatusEnum.ToDo)
                .Permit(TaskTriggerEnum.Start, TaskStatusEnum.InProgress)
                .Permit(TaskTriggerEnum.Complete, TaskStatusEnum.Complete);

            _machine.Configure(TaskStatusEnum.InProgress)
                .Permit(TaskTriggerEnum.Pause, TaskStatusEnum.Break)
                .Permit(TaskTriggerEnum.Complete, TaskStatusEnum.Complete);

            _machine.Configure(TaskStatusEnum.Break)
                .Permit(TaskTriggerEnum.Start, TaskStatusEnum.InProgress)
                .Permit(TaskTriggerEnum.Complete, TaskStatusEnum.Complete);

            _machine.Configure(TaskStatusEnum.Complete);
        }

        public bool CanFire(TaskTriggerEnum trigger) => _machine.CanFire(trigger);

        public Result Fire(TaskTriggerEnum trigger)
        {
            if (!_machine.CanFire(trigger))
            {
                return Result.Failure(
                    message: $"Cannot perform action '{trigger}' on task with status '{_task.Status}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            _machine.Fire(trigger);

            return Result.Success(
                message: $"Task status changed to '{_task.Status}'.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result TransitionTo(TaskStatusEnum targetStatus)
        {
            if (_task.Status == targetStatus)
            {
                return Result.Failure(
                    message: $"Task is already in status '{targetStatus}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var trigger = (_task.Status, targetStatus) switch
            {
                (TaskStatusEnum.ToDo, TaskStatusEnum.InProgress) => TaskTriggerEnum.Start,
                (TaskStatusEnum.ToDo, TaskStatusEnum.Complete) => TaskTriggerEnum.Complete,

                (TaskStatusEnum.InProgress, TaskStatusEnum.Break) => TaskTriggerEnum.Pause,
                (TaskStatusEnum.InProgress, TaskStatusEnum.Complete) => TaskTriggerEnum.Complete,

                (TaskStatusEnum.Break, TaskStatusEnum.InProgress) => TaskTriggerEnum.Start,
                (TaskStatusEnum.Break, TaskStatusEnum.Complete) => TaskTriggerEnum.Complete,

                _ => (TaskTriggerEnum?)null
            };

            if (!trigger.HasValue)
            {
                return Result.Failure(
                    message: $"Cannot transition task from '{_task.Status}' to '{targetStatus}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Fire(trigger.Value);
        }

        public Result CanModify()
        {
            if (_task.Status == TaskStatusEnum.Complete)
            {
                return Result.Failure(
                    message: "Cannot modify a completed task.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Task can be modified.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
