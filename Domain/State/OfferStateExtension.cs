using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Microsoft.AspNetCore.Http;

namespace Domain.State
{
    public static class OfferStateExtension
    {
        public static bool IsExpired(this Offer offer)
            => offer.ValidUntil < DateTime.UtcNow;

        public static void EnsureFreshExpirationStatus(this Offer offer)
        {
            if (offer.Status == OfferStatusEnum.Sent && offer.IsExpired())
            {
                offer.Status = OfferStatusEnum.Expired;
            }
        }

        public static Result CanEditProducts(this Offer offer)
        {
            offer.EnsureFreshExpirationStatus();

            if (offer.Status != OfferStatusEnum.Sent)
            {
                return Result.Failure(
                    message: $"Cannot edit products of an offer with status '{offer.Status}'. Only 'Sent' offers can be edited.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: "Offer products can be edited.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public static Result CanResendEmail(this Offer offer)
        {
            offer.EnsureFreshExpirationStatus();

            if (offer.Status == OfferStatusEnum.Expired || offer.IsExpired())
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

        public static Result CanExtendValidity(this Offer offer, DateTime targetDate)
        {
            if (offer.Status == OfferStatusEnum.Accepted || offer.Status == OfferStatusEnum.Rejected)
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

        public static Result CanTransitionTo(this Offer offer, OfferStatusEnum targetStatus)
        {
            offer.EnsureFreshExpirationStatus();

            if (offer.Status != OfferStatusEnum.Sent)
            {
                return Result.Failure(
                    message: $"Cannot change status of an offer with status '{offer.Status}'. Only 'Sent' offers can be modified.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            if (targetStatus != OfferStatusEnum.Accepted && targetStatus != OfferStatusEnum.Rejected)
            {
                return Result.Failure(
                    message: "Invalid target status. Status can only be changed to 'Accepted' or 'Rejected'.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            return Result.Success(
                message: $"Offer status can be changed to '{targetStatus}'.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public static Result CanDelete(this Offer offer)
        {
            if (offer.Status != OfferStatusEnum.Sent && offer.Status != OfferStatusEnum.Expired)
            {
                return Result.Failure(
                    message: $"Cannot delete an offer with status '{offer.Status}'. Only 'Sent' or 'Expired' offers can be deleted.",
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
