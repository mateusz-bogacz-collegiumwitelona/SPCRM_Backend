using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Stateless;

namespace Domain.State
{
    public class DealStateMachine
    {
        private readonly Deal _deal;
        private readonly StateMachine<DealsStatusEnum, DealTriggerEnum> _machine;

        public DealStateMachine(Deal deal)
        {
            _deal = deal;
            _machine = new StateMachine<DealsStatusEnum, DealTriggerEnum>(
                () => _deal.Status,
                newStatus => _deal.Status = newStatus
            );
            Configure();
        }

        private void Configure()
        {
            _machine.Configure(DealsStatusEnum.ToDo)
                .Permit(DealTriggerEnum.Start, DealsStatusEnum.InProgress)
                .Permit(DealTriggerEnum.Complete, DealsStatusEnum.Complete)
                .Permit(DealTriggerEnum.Cancel, DealsStatusEnum.Cancelled);

            _machine.Configure(DealsStatusEnum.InProgress)
                .Permit(DealTriggerEnum.Complete, DealsStatusEnum.Complete)
                .Permit(DealTriggerEnum.Cancel, DealsStatusEnum.Cancelled);

            _machine.Configure(DealsStatusEnum.Complete);
            _machine.Configure(DealsStatusEnum.Cancelled);
        }

        public bool CanFire(DealTriggerEnum trigger) => _machine.CanFire(trigger);

        public Result Fire(DealTriggerEnum trigger)
        {
            if (!_machine.CanFire(trigger))
            {
                return Result.Failure(
                    message: $"Cannot perform action '{trigger}' on deal with status '{_deal.Status}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            _machine.Fire(trigger);

            return Result.Success(
                message: $"Deal status successfully changed to '{_deal.Status}'.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result TransitionTo(DealsStatusEnum targetStatus)
        {
            if (_deal.Status == targetStatus)
            {
                return Result.Failure(
                    message: $"Deal is already in status '{targetStatus}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var trigger = (_deal.Status, targetStatus) switch
            {
                (DealsStatusEnum.ToDo, DealsStatusEnum.InProgress) => DealTriggerEnum.Start,
                (DealsStatusEnum.ToDo, DealsStatusEnum.Complete) => DealTriggerEnum.Complete,
                (DealsStatusEnum.ToDo, DealsStatusEnum.Cancelled) => DealTriggerEnum.Cancel,

                (DealsStatusEnum.InProgress, DealsStatusEnum.Complete) => DealTriggerEnum.Complete,
                (DealsStatusEnum.InProgress, DealsStatusEnum.Cancelled) => DealTriggerEnum.Cancel,

                _ => (DealTriggerEnum?)null
            };

            if (!trigger.HasValue)
            {
                return Result.Failure(
                    message: $"Cannot transition deal from '{_deal.Status}' to '{targetStatus}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Fire(trigger.Value);
        }

        public Result CanModify()
        {
            if (_deal.Status is DealsStatusEnum.Complete or DealsStatusEnum.Cancelled)
            {
                return Result.Failure(
                    message: $"Cannot modify a finalized deal with status '{_deal.Status}'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Deal can be modified.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
