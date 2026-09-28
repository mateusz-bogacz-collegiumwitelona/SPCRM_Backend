using Api.Request.Task;
using Api.Validators.Validators.Task;
using Domain.Constants;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class TaskCalendarValidatorTest
    {
        private readonly TaskCalendarValidator _validator = new();

        // ─── DateTo / DateFrom ────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenDateToIsAfterDateFrom_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new TaskCalendarRequest
            {
                DateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
                DateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                TaskPriority = null,
                TaskStatus = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DateTo);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDateToIsEqualToDateFrom_ShouldNotHaveValidationError()
        {
            // Arrange
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var request = new TaskCalendarRequest
            {
                DateFrom = today,
                DateTo = today,
                TaskPriority = null,
                TaskStatus = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DateTo);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Validate_WhenDateToIsBeforeDateFrom_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var request = new TaskCalendarRequest
            {
                DateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
                DateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                TaskPriority = null,
                TaskStatus = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DateTo)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        // ─── TaskPriority ─────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenTaskPriorityIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new TaskCalendarRequest
            {
                DateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
                DateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                TaskPriority = null,
                TaskStatus = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.TaskPriority);
            await Task.CompletedTask;
        }

        // ─── TaskStatus ───────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WhenTaskStatusIsNull_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new TaskCalendarRequest
            {
                DateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
                DateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                TaskPriority = null,
                TaskStatus = null
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.TaskStatus);
            await Task.CompletedTask;
        }
    }
}
