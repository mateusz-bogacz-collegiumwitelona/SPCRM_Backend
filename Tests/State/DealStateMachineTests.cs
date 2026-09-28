using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Domain.State;
using Microsoft.AspNetCore.Http;

namespace Tests.State
{
    public class DealStateMachineTests
    {
        private static DealStateMachine CreateMachine(DealsStatusEnum status, out Deal deal)
        {
            deal = new Deal { Name = "Test deal", Status = status };
            return new DealStateMachine(deal);
        }

        // ─── CanFire ─────────────────────────────────────────────────

        [Test]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Start, true)]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Complete, true)]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Cancel, true)]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Start, false)]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Complete, true)]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Cancel, true)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Start, false)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Complete, false)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Cancel, false)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Start, false)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Complete, false)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Cancel, false)]
        public async Task CanFire_ReturnsExpectedValue(
            DealsStatusEnum status, DealTriggerEnum trigger, bool expected)
        {
            var machine = CreateMachine(status, out _);

            await Assert.That(machine.CanFire(trigger)).IsEqualTo(expected);
        }

        // ─── Fire ─────────────────────────────────────────────────

        [Test]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Start, DealsStatusEnum.InProgress)]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Complete, DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.ToDo, DealTriggerEnum.Cancel, DealsStatusEnum.Cancelled)]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Complete, DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Cancel, DealsStatusEnum.Cancelled)]
        public async Task Fire_ValidTransition_ChangesStatusAndReturnsSuccess(
            DealsStatusEnum initial, DealTriggerEnum trigger, DealsStatusEnum expected)
        {
            var machine = CreateMachine(initial, out var deal);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
                await Assert.That(result.Message).Contains(expected.ToString());
                await Assert.That(deal.Status).IsEqualTo(expected);
            }
        }

        [Test]
        [Arguments(DealsStatusEnum.InProgress, DealTriggerEnum.Start)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Start)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Complete)]
        [Arguments(DealsStatusEnum.Complete, DealTriggerEnum.Cancel)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Start)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Complete)]
        [Arguments(DealsStatusEnum.Cancelled, DealTriggerEnum.Cancel)]
        public async Task Fire_InvalidTransition_ReturnsFailureAndKeepsStatus(
            DealsStatusEnum initial, DealTriggerEnum trigger)
        {
            var machine = CreateMachine(initial, out var deal);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(trigger.ToString());
                await Assert.That(result.Message).Contains(initial.ToString());
                await Assert.That(deal.Status).IsEqualTo(initial);
            }
        }


        // ─── TransitionTo ─────────────────────────────────────────────────

        [Test]
        [Arguments(DealsStatusEnum.ToDo, DealsStatusEnum.InProgress)]
        [Arguments(DealsStatusEnum.ToDo, DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.ToDo, DealsStatusEnum.Cancelled)]
        [Arguments(DealsStatusEnum.InProgress, DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.InProgress, DealsStatusEnum.Cancelled)]
        public async Task TransitionTo_ValidTransition_ChangesStatusAndReturnsSuccess(
            DealsStatusEnum initial, DealsStatusEnum target)
        {
            var machine = CreateMachine(initial, out var deal);

            var result = machine.TransitionTo(target);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
                await Assert.That(deal.Status).IsEqualTo(target);
            }
        }

        [Test]
        [Arguments(DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.InProgress)]
        [Arguments(DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.Cancelled)]
        public async Task TransitionTo_SameStatus_ReturnsFailure(DealsStatusEnum status)
        {
            var machine = CreateMachine(status, out var deal);

            var result = machine.TransitionTo(status);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains("already");
                await Assert.That(deal.Status).IsEqualTo(status);
            }
        }

        [Test]
        [Arguments(DealsStatusEnum.InProgress, DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.Complete, DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.Complete, DealsStatusEnum.InProgress)]
        [Arguments(DealsStatusEnum.Complete, DealsStatusEnum.Cancelled)]
        [Arguments(DealsStatusEnum.Cancelled, DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.Cancelled, DealsStatusEnum.InProgress)]
        [Arguments(DealsStatusEnum.Cancelled, DealsStatusEnum.Complete)]
        public async Task TransitionTo_InvalidTransition_ReturnsFailureAndKeepsStatus(
            DealsStatusEnum initial, DealsStatusEnum target)
        {
            var machine = CreateMachine(initial, out var deal);

            var result = machine.TransitionTo(target);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(initial.ToString());
                await Assert.That(result.Message).Contains(target.ToString());
                await Assert.That(deal.Status).IsEqualTo(initial);
            }
        }


        [Test]
        public async Task TransitionTo_FullHappyPath_ToDoToInProgressToComplete()
        {
            var machine = CreateMachine(DealsStatusEnum.ToDo, out var deal);

            var first = machine.TransitionTo(DealsStatusEnum.InProgress);
            var second = machine.TransitionTo(DealsStatusEnum.Complete);

            using (Assert.Multiple())
            {
                await Assert.That(first.IsSuccess).IsTrue();
                await Assert.That(second.IsSuccess).IsTrue();
                await Assert.That(deal.Status).IsEqualTo(DealsStatusEnum.Complete);
            }
        }

        [Test]
        public async Task TransitionTo_AfterFinalState_NoFurtherTransitionsAllowed()
        {
            var machine = CreateMachine(DealsStatusEnum.ToDo, out var deal);
            machine.TransitionTo(DealsStatusEnum.Cancelled);

            var result = machine.TransitionTo(DealsStatusEnum.InProgress);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(deal.Status).IsEqualTo(DealsStatusEnum.Cancelled);
            }
        }

        // ─── CanModify ─────────────────────────────────────────────────

        [Test]
        [Arguments(DealsStatusEnum.ToDo)]
        [Arguments(DealsStatusEnum.InProgress)]
        public async Task CanModify_NonFinalStatus_ReturnsSuccess(DealsStatusEnum status)
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
        [Arguments(DealsStatusEnum.Complete)]
        [Arguments(DealsStatusEnum.Cancelled)]
        public async Task CanModify_FinalStatus_ReturnsFailure(DealsStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanModify();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(status.ToString());
            }
        }
    }
}
