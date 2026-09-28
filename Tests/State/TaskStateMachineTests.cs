using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Domain.State;
using Microsoft.AspNetCore.Http;

namespace Tests.State
{
    public class TaskStateMachineTests
    {
        private static TaskStateMachine CreateMachine(TaskStatusEnum status, out Tasks task)
        {
            task = new Tasks { Title = "Test task", Description = "Test description", Status = status };
            return new TaskStateMachine(task);
        }

        // ─── CanFire ─────────────────────────────────────────────────

        [Test]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Start, true)]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Pause, false)]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Complete, true)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Start, false)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Pause, true)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Complete, true)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Start, true)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Pause, false)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Complete, true)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Start, false)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Pause, false)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Complete, false)]
        public async Task CanFire_ReturnsExpectedValue(
            TaskStatusEnum status, TaskTriggerEnum trigger, bool expected)
        {
            var machine = CreateMachine(status, out _);

            await Assert.That(machine.CanFire(trigger)).IsEqualTo(expected);
        }

        // ─── Fire ─────────────────────────────────────────────────

        [Test]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Start, TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Complete, TaskStatusEnum.Complete)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Pause, TaskStatusEnum.Break)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Complete, TaskStatusEnum.Complete)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Start, TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Complete, TaskStatusEnum.Complete)]
        public async Task Fire_ValidTransition_ChangesStatusAndReturnsSuccess(
            TaskStatusEnum initial, TaskTriggerEnum trigger, TaskStatusEnum expected)
        {
            var machine = CreateMachine(initial, out var task);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
                await Assert.That(result.Message).Contains(expected.ToString());
                await Assert.That(task.Status).IsEqualTo(expected);
            }
        }

        [Test]
        [Arguments(TaskStatusEnum.ToDo, TaskTriggerEnum.Pause)]
        [Arguments(TaskStatusEnum.InProgress, TaskTriggerEnum.Start)]
        [Arguments(TaskStatusEnum.Break, TaskTriggerEnum.Pause)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Start)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Pause)]
        [Arguments(TaskStatusEnum.Complete, TaskTriggerEnum.Complete)]
        public async Task Fire_InvalidTransition_ReturnsFailureAndKeepsStatus(
            TaskStatusEnum initial, TaskTriggerEnum trigger)
        {
            var machine = CreateMachine(initial, out var task);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(trigger.ToString());
                await Assert.That(result.Message).Contains(initial.ToString());
                await Assert.That(task.Status).IsEqualTo(initial);
            }
        }

        // ─── TransitionTo ─────────────────────────────────────────────

        [Test]
        [Arguments(TaskStatusEnum.ToDo, TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.ToDo, TaskStatusEnum.Complete)]
        [Arguments(TaskStatusEnum.InProgress, TaskStatusEnum.Break)]
        [Arguments(TaskStatusEnum.InProgress, TaskStatusEnum.Complete)]
        [Arguments(TaskStatusEnum.Break, TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.Break, TaskStatusEnum.Complete)]
        public async Task TransitionTo_ValidTransition_ChangesStatusAndReturnsSuccess(
            TaskStatusEnum initial, TaskStatusEnum target)
        {
            var machine = CreateMachine(initial, out var task);

            var result = machine.TransitionTo(target);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
                await Assert.That(task.Status).IsEqualTo(target);
            }
        }

        [Test]
        [Arguments(TaskStatusEnum.ToDo)]
        [Arguments(TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.Break)]
        [Arguments(TaskStatusEnum.Complete)]
        public async Task TransitionTo_SameStatus_ReturnsFailure(TaskStatusEnum status)
        {
            var machine = CreateMachine(status, out var task);

            var result = machine.TransitionTo(status);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains("already");
                await Assert.That(task.Status).IsEqualTo(status);
            }
        }

        [Test]
        [Arguments(TaskStatusEnum.ToDo, TaskStatusEnum.Break)]
        [Arguments(TaskStatusEnum.InProgress, TaskStatusEnum.ToDo)]
        [Arguments(TaskStatusEnum.Break, TaskStatusEnum.ToDo)]
        [Arguments(TaskStatusEnum.Complete, TaskStatusEnum.ToDo)]
        [Arguments(TaskStatusEnum.Complete, TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.Complete, TaskStatusEnum.Break)]
        public async Task TransitionTo_InvalidTransition_ReturnsFailureAndKeepsStatus(
            TaskStatusEnum initial, TaskStatusEnum target)
        {
            var machine = CreateMachine(initial, out var task);

            var result = machine.TransitionTo(target);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(initial.ToString());
                await Assert.That(result.Message).Contains(target.ToString());
                await Assert.That(task.Status).IsEqualTo(initial);
            }
        }

        [Test]
        public async Task TransitionTo_FullPathWithBreak_ToDoToInProgressToBreakToInProgressToComplete()
        {
            var machine = CreateMachine(TaskStatusEnum.ToDo, out var task);

            var results = new[]
            {
                machine.TransitionTo(TaskStatusEnum.InProgress),
                machine.TransitionTo(TaskStatusEnum.Break),
                machine.TransitionTo(TaskStatusEnum.InProgress),
                machine.TransitionTo(TaskStatusEnum.Complete)
            };

            using (Assert.Multiple())
            {
                await Assert.That(results.All(r => r.IsSuccess)).IsTrue();
                await Assert.That(task.Status).IsEqualTo(TaskStatusEnum.Complete);
            }
        }

        [Test]
        public async Task TransitionTo_AfterComplete_NoFurtherTransitionsAllowed()
        {
            var machine = CreateMachine(TaskStatusEnum.ToDo, out var task);
            machine.TransitionTo(TaskStatusEnum.Complete);

            var result = machine.TransitionTo(TaskStatusEnum.InProgress);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(task.Status).IsEqualTo(TaskStatusEnum.Complete);
            }
        }

        // ─── CanModify ─────────────────────────────────────────────

        [Test]
        [Arguments(TaskStatusEnum.ToDo)]
        [Arguments(TaskStatusEnum.InProgress)]
        [Arguments(TaskStatusEnum.Break)]
        public async Task CanModify_NonCompleteStatus_ReturnsSuccess(TaskStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanModify();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            }
        }

        [Test]
        public async Task CanModify_CompleteStatus_ReturnsFailure()
        {
            var machine = CreateMachine(TaskStatusEnum.Complete, out _);

            var result = machine.CanModify();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains("completed");
            }
        }
    }
}
