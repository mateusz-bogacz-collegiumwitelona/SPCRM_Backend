using Api.Middlewares;
using Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Tests.Middlewares
{
    public class GlobalExceptionHandlerTest
    {
        private GlobalExceptionHandler _handler = null!;
        private ILogger<GlobalExceptionHandler> _loggerMock = null!;

        private record ErrorResponseDto(bool IsSuccess, int StatusCode, string? ErrorCode, string? Message);

        [Before(Test)]
        public void Setup()
        {
            _loggerMock = new LoggerFactory().CreateLogger<GlobalExceptionHandler>();
            _handler = new GlobalExceptionHandler(_loggerMock);
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<ErrorResponseDto?> ReadResponseBodyAsync(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var json = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<ErrorResponseDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #region TryHandleAsync 

        [Test]
        public async Task TryHandleAsync_WhenOperationCanceledException_ReturnsTrueAndDoesNotWriteToResponse()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new OperationCanceledException();

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.Body.Length).IsEqualTo(0L);
        }

        [Test]
        public async Task TryHandleAsync_WhenClientAbortedRequest_ReturnsTrueAndDoesNotWriteToResponse()
        {
            // Arrange
            var context = CreateHttpContext();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            context.RequestAborted = cts.Token;

            var exception = new Exception("Any error after connection abort");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.Body.Length).IsEqualTo(0L);
        }

        [Test]
        public async Task TryHandleAsync_WhenDbUpdateConcurrencyException_Returns409Conflict()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new DbUpdateConcurrencyException("Concurrency conflict");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(409);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(response.Message).IsEqualTo("The data has been modified by another user. Please refresh and try again.");
        }

        [Test]
        public async Task TryHandleAsync_WhenKeyNotFoundException_Returns404NotFound()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new KeyNotFoundException("Key not found.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(404);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(response.Message).IsEqualTo("Resource not found.");
        }

        [Test]
        public async Task TryHandleAsync_WhenArgumentException_Returns400BadRequest()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new ArgumentException("Internal sensitive argument error details");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(400);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.BadRequest);
            await Assert.That(response.Message).IsEqualTo("Invalid argument provided.");
            await Assert.That(response.Message).DoesNotContain("Internal sensitive");
        }

        [Test]
        public async Task TryHandleAsync_WhenInvalidOperationException_Returns500InternalServerError()
        {
            var context = CreateHttpContext();
            var exception = new InvalidOperationException("Sensitive EF Core execution state error");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(500);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InternalError);
            await Assert.That(response.Message).IsEqualTo("An unexpected error occurred. Please contact support.");
            await Assert.That(response.Message).DoesNotContain("Sensitive EF Core");
        }

        [Test]
        public async Task TryHandleAsync_WhenUnhandledException_Returns500AndMasksInternalDetails()
        {
            // Arrange 
            var context = CreateHttpContext();
            var sensitiveMessage = "SELECT * FROM Users WHERE PasswordHash = 'secret_data' - connection timed out";
            var exception = new Exception(sensitiveMessage);

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(500);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InternalError);
            await Assert.That(response.Message).IsEqualTo("An unexpected error occurred. Please contact support.");

            await Assert.That(response.Message).DoesNotContain("secret_data");
        }

        #endregion
    }
}
