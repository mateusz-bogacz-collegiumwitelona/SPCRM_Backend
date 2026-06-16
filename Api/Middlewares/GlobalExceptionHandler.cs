using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<Boolean> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellation
            )
        {
            _logger.LogError(exception, "An unexpected application error occurred: {Message}", exception.Message);

            var (statusCode, message, errorCode) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found.", ErrorCodes.NotFound),
                ArgumentException argEx => (StatusCodes.Status400BadRequest, argEx.Message, ErrorCodes.BadRequest),
                InvalidOperationException invEx => (StatusCodes.Status500InternalServerError, invEx.Message, ErrorCodes.InternalError),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please contact support.", ErrorCodes.InternalError)
            };

            var result = Result<object>.Failure(
                message: message,
                statusCode: statusCode,
                errorCode: errorCode
                );

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsJsonAsync(result, cancellation);

            return true;
        }
    }
}
