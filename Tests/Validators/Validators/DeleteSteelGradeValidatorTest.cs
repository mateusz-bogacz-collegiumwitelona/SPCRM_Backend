using Api.Request.SteelGrade;
using Api.Validators.Validators.SteelGrade;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class DeleteSteelGradeValidatorTest
    {
        private readonly DeleteSteelGradeValidator _validator = new();

        // ─── Reassignments ────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenReassignmentsIsEmpty_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new DeleteSteelGradeRequest
            {
                Reassignments = new List<ProductReassignmentRequest>()
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reassignments);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenReassignmentsHasSingleItem_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new DeleteSteelGradeRequest
            {
                Reassignments = new List<ProductReassignmentRequest>
                {
                    new() { ProductId = Guid.NewGuid(), NewSteelGradeId = Guid.NewGuid() }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reassignments);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenReassignmentsHasUniqueProducts_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new DeleteSteelGradeRequest
            {
                Reassignments = new List<ProductReassignmentRequest>
                {
                    new() { ProductId = Guid.NewGuid(), NewSteelGradeId = Guid.NewGuid() },
                    new() { ProductId = Guid.NewGuid(), NewSteelGradeId = Guid.NewGuid() }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reassignments);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenReassignmentsHasDuplicateProducts_ShouldHaveDuplicateProductReassignmentErrorCode()
        {
            // Arrange
            var duplicateProductId = Guid.NewGuid();
            var request = new DeleteSteelGradeRequest
            {
                Reassignments = new List<ProductReassignmentRequest>
                {
                    new() { ProductId = duplicateProductId, NewSteelGradeId = Guid.NewGuid() },
                    new() { ProductId = duplicateProductId, NewSteelGradeId = Guid.NewGuid() }
                }
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reassignments)
                  .WithErrorCode(ErrorCodes.DuplicateProductReassignment);
            await Task.CompletedTask;
        }
    }
}
