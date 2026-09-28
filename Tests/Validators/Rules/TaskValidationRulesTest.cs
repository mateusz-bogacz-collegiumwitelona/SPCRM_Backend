using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class TaskValidationRulesTest
    {
        private class TestTaskModel
        {
            public string? Title { get; set; }
            public string? Description { get; set; }
            public DateTime DueAt { get; set; }
            public string? Priority { get; set; }
            public string? Status { get; set; }
        }

        private class TestTaskModelValidator : AbstractValidator<TestTaskModel>
        {
            public TestTaskModelValidator()
            {
                RuleFor(x => x.Title).ApplyTaskTitleRules();
                RuleFor(x => x.Description).ApplyTaskDescriptionRules();
                RuleFor(x => x.DueAt).ApplyTaskDueAtRules();
                RuleFor(x => x.Priority).ApplyTaskPriorityRules();
                RuleFor(x => x.Status).ApplyTaskStatusRules();
            }
        }

        private readonly TestTaskModelValidator _validator = new();

        // ─── ApplyTaskTitleRules ─────────────────────────────────────────────

        [Test]
        [Arguments("Spotkanie z klientem")]
        [Arguments("A")]
        public async Task ApplyTaskTitleRules_WhenValidLength_ShouldNotHaveValidationError(string validTitle)
        {
            // Arrange
            var model = new TestTaskModel { Title = validTitle };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyTaskTitleRules_WhenEmptyOrNull_ShouldHaveTaskTitleInvalidErrorCode(string? emptyTitle)
        {
            // Arrange
            var model = new TestTaskModel { Title = emptyTitle };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.TaskTitleInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTaskTitleRules_WhenExceeds150Characters_ShouldHaveTaskTitleInvalidErrorCode()
        {
            // Arrange
            var model = new TestTaskModel { Title = new string('T', 151) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.TaskTitleInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyTaskDescriptionRules ───────────────────────────────────────

        [Test]
        [Arguments("Szczegóły dotyczące zamówienia")]
        public async Task ApplyTaskDescriptionRules_WhenValidLength_ShouldNotHaveValidationError(string validDesc)
        {
            // Arrange
            var model = new TestTaskModel { Description = validDesc };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Description));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
            await Task.CompletedTask;
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyTaskDescriptionRules_WhenEmptyOrNull_ShouldHaveTaskDescriptionInvalidErrorCode(string? emptyDesc)
        {
            // Arrange
            var model = new TestTaskModel { Description = emptyDesc };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Description));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Description)
                  .WithErrorCode(ErrorCodes.TaskDescriptionInvalid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTaskDescriptionRules_WhenExceeds1000Characters_ShouldHaveTaskDescriptionInvalidErrorCode()
        {
            // Arrange
            var model = new TestTaskModel { Description = new string('D', 1001) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Description));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Description)
                  .WithErrorCode(ErrorCodes.TaskDescriptionInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyTaskDueAtRules ─────────────────────────────────────────────

        [Test]
        public async Task ApplyTaskDueAtRules_WhenDateIsInFuture_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestTaskModel { DueAt = DateTime.UtcNow.AddDays(2) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.DueAt));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DueAt);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTaskDueAtRules_WhenDateIsDefault_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var model = new TestTaskModel { DueAt = default };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.DueAt));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DueAt)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTaskDueAtRules_WhenDateIsInPast_ShouldHaveInvalidDateErrorCode()
        {
            // Arrange
            var model = new TestTaskModel { DueAt = DateTime.UtcNow.AddHours(-1) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.DueAt));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DueAt)
                  .WithErrorCode(ErrorCodes.InvalidDate);
            await Task.CompletedTask;
        }

        // ─── ApplyTaskPriorityRules ──────────────────────────────────────────

        [Test]
        public async Task ApplyTaskPriorityRules_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validPriorities = Enum.GetNames<TaskPriorityEnum>();

            foreach (var priority in validPriorities)
            {
                var modelExact = new TestTaskModel { Priority = priority };
                var modelLower = new TestTaskModel { Priority = priority.ToLower() };

                // Act
                var resultExact = _validator.TestValidate(modelExact, opt => opt.IncludeProperties(x => x.Priority));
                var resultLower = _validator.TestValidate(modelLower, opt => opt.IncludeProperties(x => x.Priority));

                // Assert
                resultExact.ShouldNotHaveValidationErrorFor(x => x.Priority);
                resultLower.ShouldNotHaveValidationErrorFor(x => x.Priority);
            }

            await Task.CompletedTask;
        }

        [Test]
        [Arguments("NieznanyPriority")]
        [Arguments("123")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyTaskPriorityRules_WhenInvalidOrEmpty_ShouldHaveTaskPriorityInvalidErrorCode(string? invalidPriority)
        {
            // Arrange
            var model = new TestTaskModel { Priority = invalidPriority };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Priority));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Priority)
                  .WithErrorCode(ErrorCodes.TaskPriorityInvalid);
            await Task.CompletedTask;
        }

        // ─── ApplyTaskStatusRules ────────────────────────────────────────────

        [Test]
        public async Task ApplyTaskStatusRules_WhenValidEnumName_ShouldNotHaveValidationError()
        {
            // Arrange
            var validStatuses = Enum.GetNames<TaskStatusEnum>();

            foreach (var status in validStatuses)
            {
                var modelExact = new TestTaskModel { Status = status };
                var modelLower = new TestTaskModel { Status = status.ToLower() };

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
        [Arguments("123")]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments(null)]
        public async Task ApplyTaskStatusRules_WhenInvalidOrEmpty_ShouldHaveTaskStatusInvalidErrorCode(string? invalidStatus)
        {
            // Arrange
            var model = new TestTaskModel { Status = invalidStatus };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Status));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Status)
                  .WithErrorCode(ErrorCodes.TaskStatusInvalid);
            await Task.CompletedTask;
        }
    }
}
