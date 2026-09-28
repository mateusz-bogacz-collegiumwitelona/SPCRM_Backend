using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class DealValidationRulesTest
    {
        private class TestDealModel
        {
            public DateTime CloseDate { get; set; }
            public int Quantity { get; set; }
            public int? NullableQuantity { get; set; }
            public long UnitPrice { get; set; }
            public long? NullableUnitPrice { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private class TestDealModelValidator : AbstractValidator<TestDealModel>
        {
            public TestDealModelValidator()
            {
                RuleFor(x => x.CloseDate).ApplyDealCloseDateRules();
                RuleFor(x => x.Quantity).ApplyDealProductQuantityRules();
                RuleFor(x => x.NullableQuantity).ApplyDealProductQuantityRules();
                RuleFor(x => x.UnitPrice).ApplyDealProductUnitPriceRules();
                RuleFor(x => x.NullableUnitPrice).ApplyDealProductUnitPriceRules();
                RuleFor(x => x.Status).ApplyDealStatusRules();
            }
        }

        private readonly TestDealModelValidator _validator = new();

        // ─── ApplyDealCloseDateRules ─────────────────────────────────────────

        [Test]
        public async Task ApplyDealCloseDateRules_WhenDateIsInFuture_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestDealModel { CloseDate = DateTime.UtcNow.AddDays(7) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.CloseDate));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.CloseDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyDealCloseDateRules_WhenDateIsDefault_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var model = new TestDealModel { CloseDate = default };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.CloseDate));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CloseDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyDealCloseDateRules_WhenDateIsInPast_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var model = new TestDealModel { CloseDate = DateTime.UtcNow.AddHours(-1) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.CloseDate));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.CloseDate)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        // ─── ApplyDealProductQuantityRules ───────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(100)]
        public async Task ApplyDealProductQuantityRules_WhenGreaterThanZero_ShouldNotHaveValidationError(int validQuantity)
        {
            // Arrange
            var model = new TestDealModel
            {
                Quantity = validQuantity,
                NullableQuantity = validQuantity
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Quantity);
                opt.IncludeProperties(x => x.NullableQuantity);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableQuantity);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(-50)]
        public async Task ApplyDealProductQuantityRules_WhenZeroOrLess_ShouldHaveDealQuantityInvalidErrorCode(int invalidQuantity)
        {
            // Arrange
            var model = new TestDealModel
            {
                Quantity = invalidQuantity,
                NullableQuantity = invalidQuantity
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Quantity);
                opt.IncludeProperties(x => x.NullableQuantity);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Quantity)
                  .WithErrorCode(ErrorCodes.DealQuantityInvalid);
            result.ShouldHaveValidationErrorFor(x => x.NullableQuantity)
                  .WithErrorCode(ErrorCodes.DealQuantityInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyDealProductUnitPriceRules ──────────────────────────────────

        [Test]
        [Arguments(1L)]
        [Arguments(50000L)]
        public async Task ApplyDealProductUnitPriceRules_WhenGreaterThanZero_ShouldNotHaveValidationError(long validPrice)
        {
            // Arrange
            var model = new TestDealModel
            {
                UnitPrice = validPrice,
                NullableUnitPrice = validPrice
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.UnitPrice);
                opt.IncludeProperties(x => x.NullableUnitPrice);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableUnitPrice);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0L)]
        [Arguments(-1L)]
        [Arguments(-1000L)]
        public async Task ApplyDealProductUnitPriceRules_WhenZeroOrLess_ShouldHaveDealProductUnitPriceInvalidErrorCode(long invalidPrice)
        {
            // Arrange
            var model = new TestDealModel
            {
                UnitPrice = invalidPrice,
                NullableUnitPrice = invalidPrice
            };

            // Act
            var result = _validator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.UnitPrice);
                opt.IncludeProperties(x => x.NullableUnitPrice);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UnitPrice)
                  .WithErrorCode(ErrorCodes.DealProductUnitPriceInvalid);
            result.ShouldHaveValidationErrorFor(x => x.NullableUnitPrice)
                  .WithErrorCode(ErrorCodes.DealProductUnitPriceInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyDealStatusRules ────────────────────────────────────────────

        [Test]
        public async Task ApplyDealStatusRules_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validStatuses = Enum.GetNames<DealsStatusEnum>();

            foreach (var status in validStatuses)
            {
                var modelExact = new TestDealModel { Status = status };
                var modelLower = new TestDealModel { Status = status.ToLower() };

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
        [Arguments("")]
        [Arguments("   ")]
        [Arguments("NieznanyStatus")]
        [Arguments("123")]
        public async Task ApplyDealStatusRules_WhenInvalidOrEmpty_ShouldHaveDealStatusInvalidErrorCode(string invalidStatus)
        {
            // Arrange
            var model = new TestDealModel { Status = invalidStatus };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Status));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Status)
                  .WithErrorCode(ErrorCodes.DealStatusInvalid);
            await Task.CompletedTask;
        }
    }
}
