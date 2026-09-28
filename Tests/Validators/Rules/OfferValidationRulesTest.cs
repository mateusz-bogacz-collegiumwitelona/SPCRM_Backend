using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class OfferValidationRulesTest
    {
        private class TestOfferModel
        {
            public Guid OfferId { get; set; }
            public DateTime? NewValidUntil { get; set; }
            public string? Status { get; set; }
        }

        private class TestOfferModelValidator : AbstractValidator<TestOfferModel>
        {
            public TestOfferModelValidator()
            {
                RuleFor(x => x.OfferId).ApplyOfferIdRules();
                RuleFor(x => x.NewValidUntil).ApplyNewValidUntilRules();
                RuleFor(x => x.Status).ApplyOfferStatusRules();
            }
        }

        private readonly TestOfferModelValidator _validator = new();

        // ─── ApplyOfferIdRules ───────────────────────────────────────────────

        [Test]
        public async Task ApplyOfferIdRules_WhenGuidIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestOfferModel { OfferId = Guid.NewGuid() };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.OfferId));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.OfferId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyOfferIdRules_WhenGuidIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var model = new TestOfferModel { OfferId = Guid.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.OfferId));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.OfferId);
            await Task.CompletedTask;
        }

        // ─── ApplyNewValidUntilRules ─────────────────────────────────────────

        [Test]
        public async Task ApplyNewValidUntilRules_WhenDateIsInFuture_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestOfferModel { NewValidUntil = DateTime.UtcNow.AddDays(7) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NewValidUntil));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NewValidUntil);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyNewValidUntilRules_WhenDateIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestOfferModel { NewValidUntil = null };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NewValidUntil));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NewValidUntil);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyNewValidUntilRules_WhenDateIsInPast_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var model = new TestOfferModel { NewValidUntil = DateTime.UtcNow.AddHours(-1) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NewValidUntil));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NewValidUntil)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        // ─── ApplyOfferStatusRules ───────────────────────────────────────────

        [Test]
        public async Task ApplyOfferStatusRules_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validStatuses = Enum.GetNames<OfferStatusEnum>();

            foreach (var status in validStatuses)
            {
                var modelExact = new TestOfferModel { Status = status };
                var modelLower = new TestOfferModel { Status = status.ToLower() };

                // Act
                var resultExact = _validator.TestValidate(modelExact, opt => opt.IncludeProperties(x => x.Status));
                var resultLower = _validator.TestValidate(modelLower, opt => opt.IncludeProperties(x => x.Status));

                // Assert
                resultExact.ShouldNotHaveValidationErrorFor(x => x.Status);
                resultLower.ShouldNotHaveValidationErrorFor(x => x.Status);
            }

            await Task.CompletedTask;
        }

        [Test]
        [Arguments("NieznanyStatus")]
        [Arguments("Invalid")]
        [Arguments("123")]
        public async Task ApplyOfferStatusRules_WhenInvalidEnumName_ShouldHaveInvalidOperationErrorCode(string invalidStatus)
        {
            // Arrange
            var model = new TestOfferModel { Status = invalidStatus };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Status));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Status)
                  .WithErrorCode(ErrorCodes.InvalidOperation);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyOfferStatusRules_WhenEmptyOrNull_ShouldHaveValidationError(string? emptyStatus)
        {
            // Arrange
            var model = new TestOfferModel { Status = emptyStatus };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Status));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Status);
            await Task.CompletedTask;
        }
    }
}
