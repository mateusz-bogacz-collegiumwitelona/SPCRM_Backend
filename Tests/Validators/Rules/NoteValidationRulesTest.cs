using Api.Validators.Rule;
using Domain.Constants;
using Domain.Enum;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Tests.Validators.Rules
{
    public class NoteValidationRulesTest
    {
        private class TestNoteModel
        {
            public Guid NoteId { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public NoteTypeEnum NoteType { get; set; }
        }

        private class TestNoteModelValidator : AbstractValidator<TestNoteModel>
        {
            public TestNoteModelValidator()
            {
                RuleFor(x => x.NoteId).ApplyNoteIdRules();
                RuleFor(x => x.Title).ApplyTitleRules();
                RuleFor(x => x.Content).ApplyContentRules();
                RuleFor(x => x.NoteType).ApplyNoteTypeRules();
            }
        }

        private readonly TestNoteModelValidator _validator = new();

        // ─── ApplyNoteIdRules ────────────────────────────────────────────────

        [Test]
        public async Task ApplyNoteIdRules_WhenGuidIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestNoteModel { NoteId = Guid.NewGuid() };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NoteId));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.NoteId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyNoteIdRules_WhenGuidIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var model = new TestNoteModel { NoteId = Guid.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NoteId));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NoteId);
            await Task.CompletedTask;
        }

        // ─── ApplyTitleRules ─────────────────────────────────────────────────

        [Test]
        [Arguments("A")]
        [Arguments("Tytuł notatki")]
        public async Task ApplyTitleRules_WhenLengthBetween1And50_ShouldNotHaveValidationError(string validTitle)
        {
            // Arrange
            var model = new TestNoteModel { Title = validTitle };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTitleRules_WhenExactly50Characters_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestNoteModel { Title = new string('T', 50) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTitleRules_WhenEmptyString_ShouldHaveNoteTitleIsNotValidErrorCode()
        {
            // Arrange
            var model = new TestNoteModel { Title = string.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.NoteTitleIsNotValid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyTitleRules_WhenExceeds50Characters_ShouldHaveNoteTitleIsNotValidErrorCode()
        {
            // Arrange
            var model = new TestNoteModel { Title = new string('T', 51) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Title));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Title)
                  .WithErrorCode(ErrorCodes.NoteTitleIsNotValid);
            await Task.CompletedTask;
        }

        // ─── ApplyContentRules ───────────────────────────────────────────────

        [Test]
        [Arguments("Krótka treść")]
        public async Task ApplyContentRules_WhenLengthBetween1And500_ShouldNotHaveValidationError(string validContent)
        {
            // Arrange
            var model = new TestNoteModel { Content = validContent };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Content));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Content);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyContentRules_WhenExactly500Characters_ShouldNotHaveValidationError()
        {
            // Arrange
            var model = new TestNoteModel { Content = new string('C', 500) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Content));

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Content);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyContentRules_WhenEmptyString_ShouldHaveNoteContentIsNotValidErrorCode()
        {
            // Arrange
            var model = new TestNoteModel { Content = string.Empty };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Content));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Content)
                  .WithErrorCode(ErrorCodes.NoteContentIsNotValid);
            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyContentRules_WhenExceeds500Characters_ShouldHaveNoteContentIsNotValidErrorCode()
        {
            // Arrange 
            var model = new TestNoteModel { Content = new string('C', 501) };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.Content));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Content)
                  .WithErrorCode(ErrorCodes.NoteContentIsNotValid);
            await Task.CompletedTask;
        }

        // ─── ApplyNoteTypeRules ──────────────────────────────────────────────

        [Test]
        public async Task ApplyNoteTypeRules_WhenValidEnumValue_ShouldNotHaveValidationError()
        {
            // Arrange
            var validEnums = Enum.GetValues<NoteTypeEnum>();

            foreach (var type in validEnums)
            {
                var model = new TestNoteModel { NoteType = type };

                // Act
                var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NoteType));

                // Assert
                result.ShouldNotHaveValidationErrorFor(x => x.NoteType);
            }

            await Task.CompletedTask;
        }

        [Test]
        public async Task ApplyNoteTypeRules_WhenInvalidEnumValue_ShouldHaveNoteTypeInvalidErrorCode()
        {
            // Arrange 
            var model = new TestNoteModel { NoteType = (NoteTypeEnum)999 };

            // Act
            var result = _validator.TestValidate(model, opt => opt.IncludeProperties(x => x.NoteType));

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NoteType)
                  .WithErrorCode(ErrorCodes.NoteTypeInvalid);
            await Task.CompletedTask;
        }
    }
}
