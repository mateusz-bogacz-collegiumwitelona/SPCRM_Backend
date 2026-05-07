using Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace Api.Configuration
{
    public class ValidationResultFactory : IFluentValidationAutoValidationResultFactory
    {
        public Task<IActionResult?> CreateActionResult(
            ActionExecutingContext context,
            ValidationProblemDetails validationProblemDetails,
            IDictionary<IValidationContext, ValidationResult> validationResults
            )
        {
            var errors = validationProblemDetails.Errors
               .SelectMany(x => x.Value)
               .ToList() ?? new List<string>();

            var result = Result<object>.Failure(
                message: "Validation failed",
                errorCode: "VALIDATION_ERROR",
                statusCode: StatusCodes.Status400BadRequest,
                errors: errors);

            return Task.FromResult<IActionResult?>(new BadRequestObjectResult(result));
        }
    }
}
