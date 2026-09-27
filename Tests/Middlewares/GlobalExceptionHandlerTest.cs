using Api.Middlewares;
using Domain.Constants;
using Domain.Exceptions.Exception;
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

        // ─── TryHandleAsync ────────────────────────────────

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

            var exception = new Exception("Dowolny błąd po przerwaniu połączenia");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.Body.Length).IsEqualTo(0L);
        }


        [Test]
        public async Task TryHandleAsync_WhenForbiddenException_Returns403WithCorrectErrorCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new ForbiddenException("Brak uprawnień do edycji.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(context.Response.ContentType).StartsWith("application/json");

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.IsSuccess).IsFalse();
            await Assert.That(response.StatusCode).IsEqualTo(403);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.UnauthorizedAccess);
            await Assert.That(response.Message).IsEqualTo("Brak uprawnień do edycji.");
        }

        [Test]
        public async Task TryHandleAsync_WhenBusinessRuleException_Returns400WithCorrectErrorCode()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new BusinessRuleException("Oferta wygasła i nie może zostać zaakceptowana.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(400);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(response.Message).IsEqualTo("Oferta wygasła i nie może zostać zaakceptowana.");
        }

        [Test]
        public async Task TryHandleAsync_WhenDataCorruptionException_Returns500WithInternalError()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new DataCorruptionException("Błąd integralności danych w encji.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(500);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InternalError);
            await Assert.That(response.Message).IsEqualTo("Błąd integralności danych w encji.");
        }


        [Test]
        public async Task TryHandleAsync_WhenDbUpdateConcurrencyException_Returns409Conflict()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new DbUpdateConcurrencyException("Konflikt edycji");

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
            var exception = new KeyNotFoundException("Klucz nie istnieje");

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
            var exception = new ArgumentException("Niepoprawny format parametru.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(400);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.BadRequest);
            await Assert.That(response.Message).IsEqualTo("Niepoprawny format parametru.");
        }

        [Test]
        public async Task TryHandleAsync_WhenInvalidOperationException_Returns500InternalServerError()
        {
            // Arrange
            var context = CreateHttpContext();
            var exception = new InvalidOperationException("Nieobsługiwany stan operacji.");

            // Act
            var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);
            var response = await ReadResponseBodyAsync(context);

            // Assert
            await Assert.That(handled).IsTrue();
            await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

            await Assert.That(response).IsNotNull();
            await Assert.That(response!.StatusCode).IsEqualTo(500);
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCodes.InternalError);
            await Assert.That(response.Message).IsEqualTo("Nieobsługiwany stan operacji.");
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
    }
}
