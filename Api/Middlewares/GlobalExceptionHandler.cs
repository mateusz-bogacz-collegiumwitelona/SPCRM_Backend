using Domain.Common;
using Domain.Constants;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

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
            if (exception is OperationCanceledException || httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Client cancelled the request: {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
                return true;
            }

            _logger.LogError(exception, "An unexpected application error occurred: {Message}", exception.Message);


            _logger.LogError(exception, "An unexpected application error occurred: {Message}", exception.Message);

            var (statusCode, message, errorCode) = exception switch
            {
                AppException appEx => (appEx.StatusCode, appEx.Message, appEx.ErrorCode),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The data has been modified by another user. Please refresh and try again.", ErrorCodes.InvalidOperation),
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
