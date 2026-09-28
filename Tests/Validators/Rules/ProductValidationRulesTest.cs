using Api.Request.Product.Contract;
using Api.Request.Promotion.Contract;
using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class ProductValidationRulesTest
    {
        private class TestProductModel
        {
            public Guid ProductId { get; set; }
            public string? Name { get; set; }
            public string? SteelGrade { get; set; }
            public string? Category { get; set; }
            public int Dimension { get; set; }
            public int? NullableDimension { get; set; }
            public int Weight { get; set; }
            public int? NullableWeight { get; set; }
            public long PricePerUnit { get; set; }
            public long? NullablePricePerUnit { get; set; }
            public int StockQuantity { get; set; }
            public int? NullableStockQuantity { get; set; }
        }

        private class TestProductModelValidator : AbstractValidator<TestProductModel>
        {
            public TestProductModelValidator()
            {
                RuleFor(x => x.ProductId).ApplyProductIdRules();
                RuleFor(x => x.Name).ApplyProductNameRules();
                RuleFor(x => x.SteelGrade).ApplyProductSteelGradeRules();
                RuleFor(x => x.Category).ApplyProductCategoryRule();
                RuleFor(x => x.Dimension).ApplyProductDimmensionRule();
                RuleFor(x => x.NullableDimension).ApplyProductDimmensionRule();
                RuleFor(x => x.Weight).ApplyProductWeightRule();
                RuleFor(x => x.NullableWeight).ApplyProductWeightRule();
                RuleFor(x => x.PricePerUnit).ApplyProductPricePerUnitRule();
                RuleFor(x => x.NullablePricePerUnit).ApplyProductPricePerUnitRule();
                RuleFor(x => x.StockQuantity).ApplyProductStockQuantityRule();
                RuleFor(x => x.NullableStockQuantity).ApplyProductStockQuantityRule();
            }
        }

        private class TestPromotionDiscountModel : IPromotionDiscountContract
        {
            public decimal? DiscountPercentage { get; set; }
            public long? PromotionalPrice { get; set; }
        }

        private class TestAddPromotionDiscountValidator : AbstractValidator<TestPromotionDiscountModel>
        {
            public TestAddPromotionDiscountValidator()
            {
                RuleFor(x => x).ApplyAddPromotionDiscountExclusiveRule();
            }
        }

        private class TestEditPromotionDiscountValidator : AbstractValidator<TestPromotionDiscountModel>
        {
            public TestEditPromotionDiscountValidator()
            {
                RuleFor(x => x).ApplyEditPromotionDiscountExclusiveRule();
            }
        }

        private class TestAddProductDimensionsModel : IAddProductDimensionsContract
        {
            public string? Category { get; set; }
            public decimal? Diameter { get; set; }
            public decimal Thickness { get; set; }
            public decimal Width { get; set; }
        }

        private class TestAddProductDimensionsValidator : AbstractValidator<TestAddProductDimensionsModel>
        {
            public TestAddProductDimensionsValidator()
            {
                this.ApplyProductCategoryDimensionsRules();
            }
        }

        private class TestEditProductDimensionsModel : IEditProductDimensionsContract
        {
            public string? Category { get; set; }
            public decimal? Diameter { get; set; }
            public decimal? Thickness { get; set; }
            public decimal? Width { get; set; }
        }

        private class TestEditProductDimensionsValidator : AbstractValidator<TestEditProductDimensionsModel>
        {
            public TestEditProductDimensionsValidator()
            {
                this.ApplyEditProductCategoryDimensionsRules();
            }
        }

        private readonly TestProductModelValidator _productValidator = new();
        private readonly TestAddPromotionDiscountValidator _addPromoValidator = new();
        private readonly TestEditPromotionDiscountValidator _editPromoValidator = new();
        private readonly TestAddProductDimensionsValidator _addDimensionsValidator = new();
        private readonly TestEditProductDimensionsValidator _editDimensionsValidator = new();

        // ─── ApplyProductIdRules ─────────────────────────────────────────────

        [Test]
        public async Task ApplyProductIdRules_WhenValidGuid_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestProductModel { ProductId = Guid.NewGuid() };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.ProductId));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProductId);
            await Task.CompletedTask;
        }

        // ─── ApplyProductNameRules ───────────────────────────────────────────

        [Test]
        public async Task ApplyProductNameRules_WhenValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestProductModel { Name = "Rura Nierdzewna 42.4" };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyProductNameRules_WhenEmptyOrNull_ShouldHaveInvalidProductNameErrorCode(string? emptyName)
        {
            // Arrange
            var model = new TestProductModel { Name = emptyName };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.InvalidProductName);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyProductNameRules_WhenExceeds150Characters_ShouldHaveInvalidProductNameErrorCode()
        {
            // Arrange
            var model = new TestProductModel { Name = new string('N', 151) };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.Name));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                  .WithErrorCode(ErrorCodes.InvalidProductName);
            await Task.CompletedTask;
        }

        // ─── ApplyProductSteelGradeRules ─────────────────────────────────────

        [Test]
        public async Task ApplyProductSteelGradeRules_WhenValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestProductModel { SteelGrade = "1.4301" };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.SteelGrade));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.SteelGrade);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyProductSteelGradeRules_WhenEmptyOrNull_ShouldHaveInvalidProductSteelGradeErrorCode(string? emptyGrade)
        {
            // Arrange
            var model = new TestProductModel { SteelGrade = emptyGrade };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.SteelGrade));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SteelGrade)
                  .WithErrorCode(ErrorCodes.InvalidProductSteelGrade);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyProductSteelGradeRules_WhenExceeds50Characters_ShouldHaveInvalidProductSteelGradeErrorCode()
        {
            // Arrange (51 znaków)
            var model = new TestProductModel { SteelGrade = new string('S', 51) };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.SteelGrade));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SteelGrade)
                  .WithErrorCode(ErrorCodes.InvalidProductSteelGrade);
            await Task.CompletedTask;
        }

        // ─── ApplyProductCategoryRule ────────────────────────────────────────

        [Test]
        public async Task ApplyProductCategoryRule_WhenValidCategoryEnum_ShouldNotHaveValidationError()
        {
            // Arrange
            var validCategories = Enum.GetNames<ProductCategoryEnum>();

            foreach (var category in validCategories)
            {
                var modelExact = new TestProductModel { Category = category };
                var modelLower = new TestProductModel { Category = category.ToLower() };

                // Act
                var resultExact = _productValidator.TestValidate(modelExact, opt => opt.IncludeProperties(x => x.Category));
                var resultLower = _productValidator.TestValidate(modelLower, opt => opt.IncludeProperties(x => x.Category));

                // Assert
                resultExact.ShouldNotHaveValidationErrorFor(x => x.Category);
                resultLower.ShouldNotHaveValidationErrorFor(x => x.Category);
            }

            await Task.CompletedTask;
        }

        [Test]
        [Arguments("NieznanaKategoria")]
        [Arguments("123")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyProductCategoryRule_WhenInvalidCategory_ShouldHaveInvalidCategoryErrorCode(string? invalidCat)
        {
            // Arrange
            var model = new TestProductModel { Category = invalidCat };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.Category));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Category)
                  .WithErrorCode(ErrorCodes.InvalidCategory);
            await Task.CompletedTask;
        }

        // ─── ApplyProductDimmensionRule ──────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(6000)]
        public async Task ApplyProductDimmensionRule_WhenGreaterThanZero_ShouldNotHaveValidationError(int validDim)
        {
            // Arrange
            var model = new TestProductModel { Dimension = validDim, NullableDimension = validDim };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Dimension);
                opt.IncludeProperties(x => x.NullableDimension);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Dimension);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableDimension);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-5)]
        public async Task ApplyProductDimmensionRule_WhenZeroOrLess_ShouldHaveInvalidProductDimmensionErrorCode(int invalidDim)
        {
            // Arrange
            var model = new TestProductModel { Dimension = invalidDim, NullableDimension = invalidDim };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Dimension);
                opt.IncludeProperties(x => x.NullableDimension);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Dimension)
                  .WithErrorCode(ErrorCodes.InvalidProductDimmension);
            result.ShouldHaveValidationErrorFor(x => x.NullableDimension)
                  .WithErrorCode(ErrorCodes.InvalidProductDimmension);
            await Task.CompletedTask;
        }

        // ─── ApplyProductWeightRule ──────────────────────────────────────────

        [Test]
        [Arguments(1)]
        [Arguments(15000)]
        public async Task ApplyProductWeightRule_WhenGreaterThanZero_ShouldNotHaveValidationError(int validWeight)
        {
            // Arrange
            var model = new TestProductModel { Weight = validWeight, NullableWeight = validWeight };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Weight);
                opt.IncludeProperties(x => x.NullableWeight);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Weight);
            result.ShouldNotHaveValidationErrorFor(x => x.NullableWeight);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0)]
        [Arguments(-10)]
        public async Task ApplyProductWeightRule_WhenZeroOrLess_ShouldHaveInvalidProductWeightErrorCode(int invalidWeight)
        {
            // Arrange
            var model = new TestProductModel { Weight = invalidWeight, NullableWeight = invalidWeight };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.Weight);
                opt.IncludeProperties(x => x.NullableWeight);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Weight)
                  .WithErrorCode(ErrorCodes.InvalidProductWeight);
            result.ShouldHaveValidationErrorFor(x => x.NullableWeight)
                  .WithErrorCode(ErrorCodes.InvalidProductWeight);
            await Task.CompletedTask;
        }

        // ─── ApplyProductPricePerUnitRule ────────────────────────────────────

        [Test]
        [Arguments(1L)]
        [Arguments(100000L)]
        public async Task ApplyProductPricePerUnitRule_WhenGreaterThanZero_ShouldNotHaveValidationError(long validPrice)
        {
            // Arrange
            var model = new TestProductModel { PricePerUnit = validPrice, NullablePricePerUnit = validPrice };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.PricePerUnit);
                opt.IncludeProperties(x => x.NullablePricePerUnit);
            });

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PricePerUnit);
            result.ShouldNotHaveValidationErrorFor(x => x.NullablePricePerUnit);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(0L)]
        [Arguments(-100L)]
        public async Task ApplyProductPricePerUnitRule_WhenZeroOrLess_ShouldHaveInvalidProductPricePerUnitErrorCode(long invalidPrice)
        {
            // Arrange
            var model = new TestProductModel { PricePerUnit = invalidPrice, NullablePricePerUnit = invalidPrice };

            // Act
            var result = _productValidator.TestValidate(model, opt =>
            {
                opt.IncludeProperties(x => x.PricePerUnit);
                opt.IncludeProperties(x => x.NullablePricePerUnit);
            });

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PricePerUnit)
                  .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);
            result.ShouldHaveValidationErrorFor(x => x.NullablePricePerUnit)
                  .WithErrorCode(ErrorCodes.InvalidProductPricePerUnit);
            await Task.CompletedTask;
        }

        // ─── ApplyProductStockQuantityRule ───────────────────────────────────

        [Test]
        [Arguments(0)]
        [Arguments(10)]
        public async Task ApplyProductStockQuantityRule_WhenNonNullableAndZeroOrGreater_ShouldNotHaveValidationError(int validQty)
        {
            // Arrange
            var model = new TestProductModel { StockQuantity = validQty };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.StockQuantity));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.StockQuantity);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments(-1)]
        [Arguments(-10)]
        public async Task ApplyProductStockQuantityRule_WhenNonNullableAndNegative_ShouldHaveInvalidProductStockQuantityErrorCode(int negativeQty)
        {
            // Arrange
            var model = new TestProductModel { StockQuantity = negativeQty };

            // Act
            var result = _productValidator.TestValidate(model, opt => opt.IncludeProperties(x => x.StockQuantity));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.StockQuantity)
                  .WithErrorCode(ErrorCodes.InvalidProductStockQuantity);
            await Task.CompletedTask;
        }

        // ─── ApplyAddPromotionDiscountExclusiveRule ──────────────────────────

        [Test]
        public async Task ApplyAddPromotionDiscountExclusiveRule_WhenOnlyDiscountPercentageProvided_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestPromotionDiscountModel { DiscountPercentage = 15m, PromotionalPrice = null };

            // Act
            var result = _addPromoValidator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyAddPromotionDiscountExclusiveRule_WhenOnlyPromotionalPriceProvided_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestPromotionDiscountModel { DiscountPercentage = null, PromotionalPrice = 50000L };

            // Act
            var result = _addPromoValidator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyAddPromotionDiscountExclusiveRule_WhenBothOrNeitherProvided_ShouldHaveDiscountPercentageAndPriceCannotBothChoiceErrorCode()
        {
            // Arrange
            var modelBoth = new TestPromotionDiscountModel { DiscountPercentage = 15m, PromotionalPrice = 50000L };
            var modelNeither = new TestPromotionDiscountModel { DiscountPercentage = null, PromotionalPrice = null };

            // Act
            var resultBoth = _addPromoValidator.TestValidate(modelBoth);
            var resultNeither = _addPromoValidator.TestValidate(modelNeither);

            // Assert
            resultBoth.ShouldHaveValidationErrorFor(x => x)
                      .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);
            resultNeither.ShouldHaveValidationErrorFor(x => x)
                         .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);
            await Task.CompletedTask;
        }

        // ─── ApplyEditPromotionDiscountExclusiveRule ─────────────────────────

        [Test]
        public async Task ApplyEditPromotionDiscountExclusiveRule_WhenNeitherOrOnlyOneProvided_ShouldNotHaveValidationError()
        {
            // Arrange
            var modelNone = new TestPromotionDiscountModel { DiscountPercentage = null, PromotionalPrice = null };
            var modelPercent = new TestPromotionDiscountModel { DiscountPercentage = 10m, PromotionalPrice = null };
            var modelPrice = new TestPromotionDiscountModel { DiscountPercentage = null, PromotionalPrice = 25000L };

            // Act & Assert
            _editPromoValidator.TestValidate(modelNone).ShouldNotHaveValidationErrorFor(x => x);
            _editPromoValidator.TestValidate(modelPercent).ShouldNotHaveValidationErrorFor(x => x);
            _editPromoValidator.TestValidate(modelPrice).ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyEditPromotionDiscountExclusiveRule_WhenBothProvided_ShouldHaveDiscountPercentageAndPriceCannotBothChoiceErrorCode()
        {
            // Arrange
            var modelBoth = new TestPromotionDiscountModel { DiscountPercentage = 20m, PromotionalPrice = 30000L };

            // Act
            var result = _editPromoValidator.TestValidate(modelBoth);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x)
                  .WithErrorCode(ErrorCodes.DiscountPercentageAndPriceCannotBothChoice);
            await Task.CompletedTask;
        }

        // ─── ApplyProductCategoryDimensionsRules ─────────────────────────────

        [Test]
        [Arguments("Pipe")]
        [Arguments("Wire")]
        public async Task ApplyProductCategoryDimensionsRules_WhenPipeOrWireHasDiameter_ShouldNotHaveValidationError(string category)
        {
            // Arrange
            var model = new TestAddProductDimensionsModel { Category = category, Diameter = 42 };

            // Act
            var result = _addDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Diameter);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("Pipe")]
        [Arguments("Wire")]
        public async Task ApplyProductCategoryDimensionsRules_WhenPipeOrWireLacksDiameter_ShouldHaveInvalidProductDimmensionErrorCode(string category)
        {
            // Arrange
            var model = new TestAddProductDimensionsModel { Category = category, Diameter = null };

            // Act
            var result = _addDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Diameter)
                  .WithErrorCode(ErrorCodes.InvalidProductDimmension);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyProductCategoryDimensionsRules_WhenBarHasWidthAndThickness_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestAddProductDimensionsModel
            {
                Category = "Bar",
                Diameter = null,
                Width = 50,
                Thickness = 10
            };

            // Act
            var result = _addDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyProductCategoryDimensionsRules_WhenBarLacksBothDiameterAndDimensions_ShouldHaveDiameterIsRequiredForPipeAndWireErrorCode()
        {
            // Arrange
            var model = new TestAddProductDimensionsModel
            {
                Category = "Bar",
                Diameter = null,
                Width = 0,
                Thickness = 0
            };

            // Act
            var result = _addDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x)
                  .WithErrorCode(ErrorCodes.DiameterIsRequiredForPipeAndWire);
            await Task.CompletedTask;
        }

        // ─── ApplyEditProductCategoryDimensionsRules ─────────────────────────

        [Test]
        [Arguments("Pipe")]
        [Arguments("Wire")]
        public async Task ApplyEditProductCategoryDimensionsRules_WhenPipeOrWireLacksDiameter_ShouldHaveDiameterIsRequiredForPipeAndWireErrorCode(string category)
        {
            // Arrange
            var model = new TestEditProductDimensionsModel { Category = category, Diameter = null };

            // Act
            var result = _editDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Diameter)
                  .WithErrorCode(ErrorCodes.DiameterIsRequiredForPipeAndWire);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyEditProductCategoryDimensionsRules_WhenBarHasValidWidthAndThickness_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestEditProductDimensionsModel
            {
                Category = "Bar",
                Width = 20,
                Thickness = 5
            };

            // Act
            var result = _editDimensionsValidator.TestValidate(model);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x);
            await Task.CompletedTask;
        }
    }
}
