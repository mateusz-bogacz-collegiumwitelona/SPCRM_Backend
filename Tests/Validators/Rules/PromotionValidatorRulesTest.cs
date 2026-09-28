using Api.Validators.Rule;
using Domain.Constants;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class PromotionValidatorRulesTest
    {
        private class TestPromotionModel
        {
            public string? Name { get; set; }
            public decimal? DiscountPercentage { get; set; }
            public long? PromotionalPrice { get; set; }
            public Guid? OptionalGuid { get; set; }
            public int? MinQuantity { get; set; }
            public int? MinWeight { get; set; }
        }

        private class TestPromotionModelValidator : AbstractValidator<TestPromotionModel>
        {
            public TestPromotionModelValidator()
            {
                RuleFor(x => x.Name).ApplyPromotionNameRules();
                RuleFor(x => x.DiscountPercentage).ApplyDiscountPercentageRule();
                RuleFor(x => x.PromotionalPrice).ApplyPromotionalPriceRule();
                RuleFor(x => x.OptionalGuid).ApplyOptionalGuidRule();
                RuleFor(x => x.MinQuantity).ApplyMinQuantityRules();
                RuleFor(x => x.MinWeight).ApplyMinWeightRules();
            }
        }

        private readonly TestPromotionModelValidator _validator = new();

        // ─── ApplyPromotionNameRules ─────────────────────────────────────────

        [Test]
        [Arguments("Wiosenna Promocja")]
        [Arguments("A")]
        public async Task ApplyPromotionNameRules_WhenValidLength_ShouldNotHaveValidationError(string validName)
        {
            // Arrange
            var model = new TestPromotionModel { Name = validName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyPromotionNameRules_WhenEmptyOrNull_ShouldHaveValidationError(string? emptyName)
        {
            // Arrange
            var model = new TestPromotionModel { Name = emptyName };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyPromotionNameRules_WhenExceeds150Characters_ShouldHaveInvalidPromotionNameErrorCode()
        {
            // Arrange
            var model = new TestPromotionModel { Name = new string('P', 151) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.InvalidPromotionName);
            await Task.CompletedTask;
        }

        // ─── ApplyDiscountPercentageRule ─────────────────────────────────────

        [Test]
        [Arguments(1.0)]
        [Arguments(50.0)]
        [Arguments(100.0)]
        [Arguments(null)]
        public async Task ApplyDiscountPercentageRule_WhenWithinRangeOrNull_ShouldNotHaveValidationError(double? validPercent)
        {
            // Arrange
            var model = new TestPromotionModel { DiscountPercentage = (decimal?)validPercent };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.DiscountPercentage));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DiscountPercentage);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0.0)]
        [Arguments(-5.0)]
        [Arguments(100.01)]
        [Arguments(150.0)]
        public async Task ApplyDiscountPercentageRule_WhenOutOfRange_ShouldHaveInvalidPromotionDiscountErrorCode(double invalidPercent)
        {
            // Arrange
            var model = new TestPromotionModel { DiscountPercentage = (decimal)invalidPercent };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.DiscountPercentage));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DiscountPercentage)
                  .WithErrorCode(ErrorCodes.InvalidPromotionDiscount);
            await Task.CompletedTask;
        }

        // ─── ApplyPromotionalPriceRule ───────────────────────────────────────

        [Test]
        [Arguments(1L)]
        [Arguments(50000L)]
        [Arguments(null)]
        public async Task ApplyPromotionalPriceRule_WhenGreaterThanZeroOrNull_ShouldNotHaveValidationError(long? validPrice)
        {
            // Arrange
            var model = new TestPromotionModel { PromotionalPrice = validPrice };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PromotionalPrice));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PromotionalPrice);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0L)]
        [Arguments(-100L)]
        public async Task ApplyPromotionalPriceRule_WhenZeroOrLess_ShouldHaveInvalidPromotionPriceErrorCode(long invalidPrice)
        {
            // Arrange
            var model = new TestPromotionModel { PromotionalPrice = invalidPrice };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.PromotionalPrice));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PromotionalPrice)
                  .WithErrorCode(ErrorCodes.InvalidPromotionPrice);
            await Task.CompletedTask;
        }

        // ─── ApplyOptionalGuidRule ───────────────────────────────────────────

        [Test]
        public async Task ApplyOptionalGuidRule_WhenGuidIsValidOrNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var modelValid = new TestPromotionModel { OptionalGuid = Guid.NewGuid() };
            var modelNull = new TestPromotionModel { OptionalGuid = null };

            // Act
            var resultValid = _validator.TestValidate(modelValid, opt => opt.IncludeProperties(x => x.OptionalGuid));
            var resultNull = _validator.TestValidate(modelNull, opt => opt.IncludeProperties(x => x.OptionalGuid));

            // Assert
            resultValid.ShouldNotHaveValidationErrorFor(x => x.OptionalGuid);
            resultNull.ShouldNotHaveValidationErrorFor(x => x.OptionalGuid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyOptionalGuidRule_WhenGuidIsEmpty_ShouldHaveGuidInvalidErrorCode()
        {
            // Arrange
            var model = new TestPromotionModel { OptionalGuid = Guid.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.OptionalGuid));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.OptionalGuid)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyMinQuantityRules ───────────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(100)]
        [Arguments(null)]
        public async Task ApplyMinQuantityRules_WhenGreaterThanZeroOrNull_ShouldNotHaveValidationError(int? validQty)
        {
            // Arrange
            var model = new TestPromotionModel { MinQuantity = validQty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.MinQuantity));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.MinQuantity);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-5)]
        public async Task ApplyMinQuantityRules_WhenZeroOrLess_ShouldHaveInvalidPromotioMinQuantityErrorCode(int invalidQty)
        {
            // Arrange
            var model = new TestPromotionModel { MinQuantity = invalidQty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.MinQuantity));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.MinQuantity)
                  .WithErrorCode(ErrorCodes.InvalidPromotioMinQuantity);
            await Task.CompletedTask;
        }

        // ─── ApplyMinWeightRules ─────────────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(5000)]
        [Arguments(null)]
        public async Task ApplyMinWeightRules_WhenGreaterThanZeroOrNull_ShouldNotHaveValidationError(int? validWeight)
        {
            // Arrange
            var model = new TestPromotionModel { MinWeight = validWeight };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.MinWeight));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.MinWeight);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-10)]
        public async Task ApplyMinWeightRules_WhenZeroOrLess_ShouldHaveInvalidPromotioMinWeightErrorCode(int invalidWeight)
        {
            // Arrange
            var model = new TestPromotionModel { MinWeight = invalidWeight };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.MinWeight));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.MinWeight)
                  .WithErrorCode(ErrorCodes.InvalidPromotioMinWeight);
            await Task.CompletedTask;
        }
    }
}
