using Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        public async ValueTask<Boolean> TryHandleAsync(
            HttpContext httpContext,
            Exception exceptionm,
            CancellationToken cancellation
            )
        {
            var (statusCode, message) = exceptionm switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
                ArgumentException argEx => (StatusCodes.Status400BadRequest, argEx.Message),
                InvalidOperationException invEx => (StatusCodes.Status400BadRequest, invEx.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please contact support.")
            };

            var result = Result<object>.Failure(
                message: message,
                statusCode: statusCode,
                errors: new List<string> { exceptionm.Message }
                );

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(result, cancellation);

            return true;
        }
    }
}
