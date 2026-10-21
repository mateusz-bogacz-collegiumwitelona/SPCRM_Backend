using Api.Factory;
using Domain.Common;
using Domain.Constants;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;


namespace Tests.Factory
{
    public class ValidationResultFactoryTests
    {
        private ValidationResultFactory _factory = null!;

        [Before(Test)]
        public void Setup()
        {
            _factory = new ValidationResultFactory();
        }

        private static ActionExecutingContext CreateActionExecutingContext()
        {
            var actionContext = new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor()
            );

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object()
            );
        }

        [Test]
        public async Task CreateActionResult_WhenValidationResultsHaveErrorCode_UsesFirstNonEmptyErrorCode()
        {
            // Arrange
            var context = CreateActionExecutingContext();

            var validationProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                { "Email", new[] { "Email jest niepoprawny" } },
                { "Password", new[] { "Hasło jest za krótkie" } }
            });

            var validationResult1 = new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Email jest niepoprawny") { ErrorCode = "VAL_002" }
            });

            var validationResult2 = new ValidationResult(new[]
            {
                new ValidationFailure("Password", "Hasło jest za krótkie") { ErrorCode = "VAL_003" }
            });

            var validationContextMock = new ValidationContext<object>(new object());
            var validationResults = new Dictionary<IValidationContext, ValidationResult>
            {
                { validationContextMock, validationResult1 },
                { new ValidationContext<object>(new object()), validationResult2 }
            };

            // Act
            var actionResult = await _factory.CreateActionResult(context, validationProblemDetails, validationResults);

            // Assert
            var badRequestResult = actionResult as BadRequestObjectResult;
            await Assert.That(badRequestResult).IsNotNull();
            await Assert.That(badRequestResult!.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            var result = badRequestResult.Value as Result<object>;
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("Validation failed");
            await Assert.That(result.ErrorCode).IsEqualTo("VAL_002");

            await Assert.That(result.Errors).IsNotNull();
            await Assert.That(result.Errors!.Count).IsEqualTo(2);
            await Assert.That(result.Errors).Contains("Email jest niepoprawny");
            await Assert.That(result.Errors).Contains("Hasło jest za krótkie");
        }

        [Test]
        public async Task CreateActionResult_WhenNoErrorCodeDefined_FallsBackToDefaultValidationError()
        {
            // Arrange
            var context = CreateActionExecutingContext();

            var validationProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Pole jest wymagane" } }
            });

            var validationResult = new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Pole jest wymagane") { ErrorCode = string.Empty }
            });

            var validationResults = new Dictionary<IValidationContext, ValidationResult>
            {
                { new ValidationContext<object>(new object()), validationResult }
            };

            // Act
            var actionResult = await _factory.CreateActionResult(context, validationProblemDetails, validationResults);

            // Assert
            var badRequestResult = actionResult as BadRequestObjectResult;
            await Assert.That(badRequestResult).IsNotNull();

            var result = badRequestResult!.Value as Result<object>;
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ErrorCode).IsEqualTo(ErrorCodes.ValidationError);
        }

        [Test]
        public async Task CreateActionResult_WhenValidationResultsDictionaryIsEmpty_FallsBackToDefaultValidationError()
        {
            // Arrange
            var context = CreateActionExecutingContext();
            var validationProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>());
            var validationResults = new Dictionary<IValidationContext, ValidationResult>();

            // Act
            var actionResult = await _factory.CreateActionResult(context, validationProblemDetails, validationResults);

            // Assert
            var badRequestResult = actionResult as BadRequestObjectResult;
            await Assert.That(badRequestResult).IsNotNull();

            var result = badRequestResult!.Value as Result<object>;
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ErrorCode).IsEqualTo(ErrorCodes.ValidationError);
            await Assert.That(result.Errors).IsEmpty();
        }
    }
}
