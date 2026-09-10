using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Domain.State.Helpers;
using Microsoft.AspNetCore.Http;
using Stateless;

namespace Domain.State
{
    public class OfferStateMachine
    {
        private readonly Offer _offer;
        private readonly StateMachine<OfferStatusEnum, OfferTriggerEnum> _machine;

        public OfferStateMachine(Offer offer)
        {
            _offer = offer;

            EnsureFreshExpirationStatus();

            _machine = new StateMachine<OfferStatusEnum, OfferTriggerEnum>(
                () => _offer.Status,
                newStatus => _offer.Status = newStatus
            );

            Configure();
        }

        private void Configure()
        {
            _machine.Configure(OfferStatusEnum.Sent)
                .PermitIf(
                    OfferTriggerEnum.Accept,
                    OfferStatusEnum.Accepted,
                    () => !_offer.IsExpired(),
                    "Cannot accept an expired offer."
                )
                .Permit(OfferTriggerEnum.Reject, OfferStatusEnum.Rejected)
                .Permit(OfferTriggerEnum.Expire, OfferStatusEnum.Expired);

            _machine.Configure(OfferStatusEnum.Accepted);
            _machine.Configure(OfferStatusEnum.Rejected);
            _machine.Configure(OfferStatusEnum.Expired);
        }

        public void EnsureFreshExpirationStatus()
        {
            if (_offer.Status == OfferStatusEnum.Sent && _offer.IsExpired())
            {
                _offer.Status = OfferStatusEnum.Expired;
            }
        }

        public Result Fire(OfferTriggerEnum trigger)
        {
            EnsureFreshExpirationStatus();

            if (!_machine.CanFire(trigger))
            {
                return Result.Failure(
                    message: $"Cannot change status of an offer with status '{_offer.Status}'. Operation '{trigger}' is invalid.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            _machine.Fire(trigger);

            return Result.Success(
                message: $"Offer status changed to '{_offer.Status}'.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public bool CanFire(OfferTriggerEnum trigger)
        {
            EnsureFreshExpirationStatus();
            return _machine.CanFire(trigger);
        }

        public Result CanEditProducts()
        {
            EnsureFreshExpirationStatus();

            if (_offer.Status != OfferStatusEnum.Sent)
            {
                return Result.Failure(
                    message: $"Cannot edit products of an offer with status '{_offer.Status}'. Only 'Sent' offers can be edited.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Offer products can be edited.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result CanResendEmail()
        {
            EnsureFreshExpirationStatus();

            if (_offer.Status != OfferStatusEnum.Sent)
            {
                return Result.Failure(
                    message: $"Cannot resend email for an offer with status '{_offer.Status}'. Only 'Sent' offers can be resent.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (_offer.IsExpired())
            {
                return Result.Failure(
                    message: "Cannot resend an expired offer. Please extend validity first.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Offer email can be resent.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result CanExtendValidity(DateTime targetDate)
        {
            if (_offer.Status == OfferStatusEnum.Accepted || _offer.Status == OfferStatusEnum.Rejected)
            {
                return Result.Failure(
                    message: "Cannot extend validity of an accepted or rejected offer.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (targetDate <= DateTime.UtcNow)
            {
                return Result.Failure(
                    message: "New validity date must be in the future.",
                    errorCode: ErrorCodes.InvalidDate,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Offer validity can be extended.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public Result CanDelete()
        {
            if (_offer.Status != OfferStatusEnum.Sent && _offer.Status != OfferStatusEnum.Expired)
            {
                return Result.Failure(
                    message: $"Cannot delete an offer with status '{_offer.Status}'. Only 'Sent' or 'Expired' offers can be deleted.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Offer can be deleted.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
