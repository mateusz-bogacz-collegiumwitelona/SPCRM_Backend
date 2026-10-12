using Domain.Common;
using Domain.Constants;
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
            var firstErrorCode = validationResults
                .SelectMany(x => x.Value.Errors)
                .Select(x => x.ErrorCode)
                .FirstOrDefault(code => !string.IsNullOrEmpty(code)) ?? ErrorCodes.ValidationError;

            var errors = validationProblemDetails.Errors
               .SelectMany(x => x.Value)
               .ToList() ?? new List<string>();

            var result = Result<object>.Failure(
                message: "Validation failed",
                errorCode: firstErrorCode,
                statusCode: StatusCodes.Status400BadRequest,
                errors: errors);

            return Task.FromResult<IActionResult?>(new BadRequestObjectResult(result));
        }
    }
}
