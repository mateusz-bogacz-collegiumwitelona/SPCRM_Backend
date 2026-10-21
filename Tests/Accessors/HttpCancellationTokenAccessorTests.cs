using Microsoft.AspNetCore.Http;
using Services.Accessors;

namespace Tests.Accessors
{
    public class HttpCancellationTokenAccessorTests
    {
        private class FakeHttpContextAccessor : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; }
        }

        [Test]
        public async Task Token_WhenHttpContextIsNull_ReturnsCancellationTokenNone()
        {
            // Arrange
            var accessorMock = new FakeHttpContextAccessor { HttpContext = null };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token).IsEqualTo(CancellationToken.None);
            await Assert.That(token.CanBeCanceled).IsFalse();
            await Assert.That(token.IsCancellationRequested).IsFalse();
        }

        [Test]
        public async Task Token_WhenHttpContextExistsAndNotAborted_ReturnsActiveToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token
            };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token.CanBeCanceled).IsTrue();
            await Assert.That(token.IsCancellationRequested).IsFalse();
            await Assert.That(token).IsEqualTo(cts.Token);
        }

        [Test]
        public async Task Token_WhenRequestIsAborted_ReflectsCancellationState()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token
            };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            cts.Cancel();

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token.IsCancellationRequested).IsTrue();
            await Assert.That(token).IsEqualTo(cts.Token);
        }
    }
}
