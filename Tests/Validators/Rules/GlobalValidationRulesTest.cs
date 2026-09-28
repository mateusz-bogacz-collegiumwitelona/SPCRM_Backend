using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class GlobalValidationRulesTest
    {
        private class TestGlobalModel
        {
            public Guid RequiredGuid { get; set; }
            public Guid? NullableGuid { get; set; }
            public int? PageNumber { get; set; }
            public int? PageSize { get; set; }
            public string? Language { get; set; }
        }

        private class TestGlobalModelValidator : AbstractValidator<TestGlobalModel>
        {
            public TestGlobalModelValidator()
            {
                RuleFor(x => x.RequiredGuid).ApplyValidGuidRule();
                RuleFor(x => x.NullableGuid).ApplyValidGuidRule();
                RuleFor(x => x.PageNumber).ApplyPageNumberRules();
                RuleFor(x => x.PageSize).ApplyPageSizeRules();
                RuleFor(x => x.Language).ApplyLanguageRules();
            }
        }

        private readonly TestGlobalModelValidator _validator = new();

        // ─── ApplyValidGuidRule ──────────────────────────────────────────────

        [Test]
        public async Task ApplyValidGuidRule_WhenGuidIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var validId = Guid.NewGuid();
            var model = new TestGlobalModel
            {
                RequiredGuid = validId,
                NullableGuid = validId
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.RequiredGuid);
                opt.IncludeProperties(x => x.NullableGuid);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.RequiredGuid);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableGuid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyValidGuidRule_WhenNullableGuidIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestGlobalModel { NullableGuid = null };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NullableGuid));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NullableGuid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyValidGuidRule_WhenRequiredGuidIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var model = new TestGlobalModel { RequiredGuid = Guid.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.RequiredGuid));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.RequiredGuid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyValidGuidRule_WhenNullableGuidIsEmpty_ShouldHaveGuidInvalidErrorCode()
        {
            // Arrange
            var model = new TestGlobalModel { NullableGuid = Guid.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NullableGuid));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NullableGuid)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyPageNumberRules ────────────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(10)]
        [Arguments(null)]
        public async Task ApplyPageNumberRules_WhenValid_ShouldNotHaveValidationError(int? validPageNumber)
        {
            // Arrange
            var model = new TestGlobalModel { PageNumber = validPageNumber };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PageNumber));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PageNumber);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task ApplyPageNumberRules_WhenZeroOrLess_ShouldHavePageNumberInvalidErrorCode(int invalidPageNumber)
        {
            // Arrange
            var model = new TestGlobalModel { PageNumber = invalidPageNumber };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PageNumber));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageNumber)
                  .WithErrorCode(ErrorCodes.PageNumberInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyPageSizeRules ──────────────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(50)]
        [Arguments(100)]
        [Arguments(null)]
        public async Task ApplyPageSizeRules_WhenWithinRangeOrNull_ShouldNotHaveValidationError(int? validPageSize)
        {
            // Arrange
            var model = new TestGlobalModel { PageSize = validPageSize };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PageSize));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(101)]
        [Arguments(200)]
        public async Task ApplyPageSizeRules_WhenOutOfRange_ShouldHavePageSizeInvalidErrorCode(int invalidPageSize)
        {
            // Arrange
            var model = new TestGlobalModel { PageSize = invalidPageSize };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PageSize));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageSize)
                  .WithErrorCode(ErrorCodes.PageSizeInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyLanguageRules ──────────────────────────────────────────────

        [Test]
        [Arguments("pl")]
        [Arguments("PL")]
        [Arguments("en")]
        [Arguments("EN")]
        [Arguments("Pl")]
        [Arguments(null)]
        [Arguments("")]
        [Arguments("   ")]
        public async Task ApplyLanguageRules_WhenAllowedLanguageOrWhitespace_ShouldNotHaveValidationError(string? validLang)
        {
            // Arrange
            var model = new TestGlobalModel { Language = validLang };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Language));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Language);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("de")]
        [Arguments("fr")]
        [Arguments("es")]
        [Arguments("polish")]
        public async Task ApplyLanguageRules_WhenUnsupportedLanguage_ShouldHaveInvalidOperationErrorCode(string invalidLang)
        {
            // Arrange
            var model = new TestGlobalModel { Language = invalidLang };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Language));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Language)
                  .WithErrorCode(ErrorCodes.InvalidOperation);
            await Task.CompletedTask;
        }
    }
}
