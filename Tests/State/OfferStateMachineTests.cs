using Domain.Constants;
using Domain.Enum;
using Domain.Enum.Triggers;
using Domain.Models;
using Domain.State;
using Microsoft.AspNetCore.Http;

namespace Tests.State
{
    public class OfferStateMachineTests
    {
        private static OfferStateMachine CreateMachine(
            OfferStatusEnum status, out Offer offer, bool expired = false)
        {
            offer = new Offer
            {
                Status = status,
                ValidUntil = expired ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7),
                CreatedByUserId = Guid.NewGuid(),
            };
            return new OfferStateMachine(offer);
        }

        // ─── Constructor / EnsureFreshExpirationStatus ─────────────────

        [Test]
        public async Task Constructor_SentAndExpired_SetsStatusToExpired()
        {
            CreateMachine(OfferStatusEnum.Sent, out var offer, expired: true);

            await Assert.That(offer.Status).IsEqualTo(OfferStatusEnum.Expired);
        }

        [Test]
        public async Task Constructor_SentAndNotExpired_KeepsSentStatus()
        {
            CreateMachine(OfferStatusEnum.Sent, out var offer);

            await Assert.That(offer.Status).IsEqualTo(OfferStatusEnum.Sent);
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        public async Task Constructor_FinalStatusAndExpired_KeepsStatus(OfferStatusEnum status)
        {
            CreateMachine(status, out var offer, expired: true);

            await Assert.That(offer.Status).IsEqualTo(status);
        }

        [Test]
        public async Task EnsureFreshExpirationStatus_OfferExpiredAfterCreation_SetsStatusToExpired()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out var offer);
            offer.ValidUntil = DateTime.UtcNow.AddDays(-1);

            machine.EnsureFreshExpirationStatus();

            await Assert.That(offer.Status).IsEqualTo(OfferStatusEnum.Expired);
        }

        // ─── CanFire ─────────────────────────────────────────────────

        [Test]
        [Arguments(OfferStatusEnum.Sent, OfferTriggerEnum.Accept, true)]
        [Arguments(OfferStatusEnum.Sent, OfferTriggerEnum.Reject, true)]
        [Arguments(OfferStatusEnum.Sent, OfferTriggerEnum.Expire, true)]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Accept, false)]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Reject, false)]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Expire, false)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Accept, false)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Reject, false)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Expire, false)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Accept, false)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Reject, false)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Expire, false)]
        public async Task CanFire_ReturnsExpectedValue(
            OfferStatusEnum status, OfferTriggerEnum trigger, bool expected)
        {
            var machine = CreateMachine(status, out _);

            await Assert.That(machine.CanFire(trigger)).IsEqualTo(expected);
        }

        [Test]
        [Arguments(OfferTriggerEnum.Accept)]
        [Arguments(OfferTriggerEnum.Reject)]
        [Arguments(OfferTriggerEnum.Expire)]
        public async Task CanFire_SentButExpired_ReturnsFalse(OfferTriggerEnum trigger)
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _, expired: true);

            await Assert.That(machine.CanFire(trigger)).IsFalse();
        }

        // ─── Fire ─────────────────────────────────────────────────

        [Test]
        [Arguments(OfferTriggerEnum.Accept, OfferStatusEnum.Accepted)]
        [Arguments(OfferTriggerEnum.Reject, OfferStatusEnum.Rejected)]
        [Arguments(OfferTriggerEnum.Expire, OfferStatusEnum.Expired)]
        public async Task Fire_ValidTransition_ChangesStatusAndReturnsSuccess(
            OfferTriggerEnum trigger, OfferStatusEnum expected)
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out var offer);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
                await Assert.That(result.Message).Contains(expected.ToString());
                await Assert.That(offer.Status).IsEqualTo(expected);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Accept)]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Reject)]
        [Arguments(OfferStatusEnum.Accepted, OfferTriggerEnum.Expire)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Accept)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Reject)]
        [Arguments(OfferStatusEnum.Rejected, OfferTriggerEnum.Expire)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Accept)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Reject)]
        [Arguments(OfferStatusEnum.Expired, OfferTriggerEnum.Expire)]
        public async Task Fire_InvalidTransition_ReturnsFailureAndKeepsStatus(
            OfferStatusEnum initial, OfferTriggerEnum trigger)
        {
            var machine = CreateMachine(initial, out var offer);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(trigger.ToString());
                await Assert.That(result.Message).Contains(initial.ToString());
                await Assert.That(offer.Status).IsEqualTo(initial);
            }
        }

        [Test]
        [Arguments(OfferTriggerEnum.Accept)]
        [Arguments(OfferTriggerEnum.Reject)]
        public async Task Fire_SentButExpired_ReturnsFailureAndStatusIsExpired(OfferTriggerEnum trigger)
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out var offer, expired: true);

            var result = machine.Fire(trigger);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(offer.Status).IsEqualTo(OfferStatusEnum.Expired);
            }
        }

        [Test]
        public async Task Fire_OfferExpiredAfterCreation_AcceptFails()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out var offer);
            offer.ValidUntil = DateTime.UtcNow.AddDays(-1);

            var result = machine.Fire(OfferTriggerEnum.Accept);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(offer.Status).IsEqualTo(OfferStatusEnum.Expired);
            }
        }

        // ─── CanEditProducts ─────────────────────────────────────────

        [Test]
        public async Task CanEditProducts_SentStatus_ReturnsSuccess()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _);

            var result = machine.CanEditProducts();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task CanEditProducts_NonSentStatus_ReturnsFailure(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanEditProducts();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(status.ToString());
            }
        }

        [Test]
        public async Task CanEditProducts_SentButExpired_ReturnsFailure()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _, expired: true);

            var result = machine.CanEditProducts();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.Message).Contains(OfferStatusEnum.Expired.ToString());
            }
        }

        // ─── CanResendEmail ─────────────────────────────────────────

        [Test]
        public async Task CanResendEmail_SentAndNotExpired_ReturnsSuccess()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _);

            var result = machine.CanResendEmail();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task CanResendEmail_NonSentStatus_ReturnsFailure(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanResendEmail();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(status.ToString());
            }
        }

        [Test]
        public async Task CanResendEmail_SentButExpired_ReturnsFailure()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _, expired: true);

            var result = machine.CanResendEmail();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            }
        }

        // ─── CanExtendValidity ─────────────────────────────────────────

        [Test]
        [Arguments(OfferStatusEnum.Sent)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task CanExtendValidity_ExtendableStatusAndFutureDate_ReturnsSuccess(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanExtendValidity(DateTime.UtcNow.AddDays(30));

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        public async Task CanExtendValidity_AcceptedOrRejected_ReturnsFailure(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanExtendValidity(DateTime.UtcNow.AddDays(30));

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        public async Task CanExtendValidity_AcceptedOrRejectedAndPastDate_ReturnsInvalidOperation(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanExtendValidity(DateTime.UtcNow.AddDays(-1));

            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        [Arguments(OfferStatusEnum.Sent)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task CanExtendValidity_PastDate_ReturnsInvalidDate(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanExtendValidity(DateTime.UtcNow.AddDays(-1));

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidDate);
            }
        }

        [Test]
        public async Task CanExtendValidity_DateEqualToNow_ReturnsInvalidDate()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _);

            var result = machine.CanExtendValidity(DateTime.UtcNow);

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidDate);
            }
        }

        // ─── CanDelete ─────────────────────────────────────────────────

        [Test]
        [Arguments(OfferStatusEnum.Sent)]
        [Arguments(OfferStatusEnum.Expired)]
        public async Task CanDelete_SentOrExpired_ReturnsSuccess(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanDelete();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsTrue();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            }
        }

        [Test]
        [Arguments(OfferStatusEnum.Accepted)]
        [Arguments(OfferStatusEnum.Rejected)]
        public async Task CanDelete_AcceptedOrRejected_ReturnsFailure(OfferStatusEnum status)
        {
            var machine = CreateMachine(status, out _);

            var result = machine.CanDelete();

            using (Assert.Multiple())
            {
                await Assert.That(result.IsSuccess).IsFalse();
                await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
                await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
                await Assert.That(result.Message).Contains(status.ToString());
            }
        }

        [Test]
        public async Task CanDelete_SentButExpired_ReturnsSuccess()
        {
            var machine = CreateMachine(OfferStatusEnum.Sent, out _, expired: true);

            var result = machine.CanDelete();

            await Assert.That(result.IsSuccess).IsTrue();
        }
    }
}
