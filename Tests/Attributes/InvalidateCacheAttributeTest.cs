using Api.Attributes;
using Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tests.Fakes;

namespace Tests.Attributes
{
    public class InvalidateCacheAttributeTest
    {
        private FakeOutputCacheStore _cacheStore = null!;
        private DefaultHttpContext _httpContext = null!;
        private ActionContext _actionContext = null!;

        [Before(Test)]
        public void Setup()
        {
            _cacheStore = new FakeOutputCacheStore();

            var services = new ServiceCollection();
            services.AddSingleton<IOutputCacheStore>(_cacheStore);

            _httpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            };

            _actionContext = new ActionContext(
                _httpContext,
                new RouteData(),
                new ActionDescriptor()
            );
        }

        private ActionExecutingContext CreateExecutingContext()
        {
            return new ActionExecutingContext(
                _actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object()
            );
        }

        private static ActionExecutionDelegate CreateNextDelegate(IActionResult? result, Exception? exception = null)
        {
            return () =>
            {
                var executedContext = new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(),
                    new object()
                )
                {
                    Result = result,
                    Exception = exception
                };

                return Task.FromResult(executedContext);
            };
        }

        // ─── OnActionExecutionAsync ──────────────────────────────

        [Test]
        public async Task OnActionExecutionAsync_WhenSingleDirectTagProvided_EvictsThatTag()
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute("custom_cache_tag");
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new OkObjectResult(new { message = "ok" }));

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).Count().IsEqualTo(1);
            await Assert.That(_cacheStore.EvictedTags).Contains("custom_cache_tag");
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenGroupTagProvided_ResolvesAllTagsFromArrayViaReflection()
        {
            // Arrange 
            var attribute = new InvalidateCacheAttribute(nameof(CacheTags.CompanyAll));
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new OkObjectResult(new { message = "ok" }));

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert 
            await Assert.That(_cacheStore.EvictedTags).Count().IsEqualTo(CacheTags.CompanyAll.Length);

            foreach (var expectedTag in CacheTags.CompanyAll)
            {
                await Assert.That(_cacheStore.EvictedTags).Contains(expectedTag);
            }
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenMixOfGroupAndDirectTagsProvided_EvictsAllCorrectly()
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute(nameof(CacheTags.CurrencyAll), "standalone_tag");
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new ObjectResult(new { }) { StatusCode = StatusCodes.Status201Created });

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            var expectedCount = CacheTags.CurrencyAll.Length + 1;
            await Assert.That(_cacheStore.EvictedTags).Count().IsEqualTo(expectedCount);

            foreach (var currencyTag in CacheTags.CurrencyAll)
            {
                await Assert.That(_cacheStore.EvictedTags).Contains(currencyTag);
            }
            await Assert.That(_cacheStore.EvictedTags).Contains("standalone_tag");
        }

        [Test]
        [Arguments(StatusCodes.Status200OK)]
        [Arguments(StatusCodes.Status201Created)]
        [Arguments(StatusCodes.Status204NoContent)]
        public async Task OnActionExecutionAsync_WhenResponseIsSuccess2xx_EvictsTags(int statusCode)
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute("test_tag");
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new ObjectResult(new { }) { StatusCode = statusCode });

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).Count().IsEqualTo(1);
            await Assert.That(_cacheStore.EvictedTags).Contains("test_tag");
        }

        [Test]
        [Arguments(StatusCodes.Status400BadRequest)]
        [Arguments(StatusCodes.Status401Unauthorized)]
        [Arguments(StatusCodes.Status403Forbidden)]
        [Arguments(StatusCodes.Status404NotFound)]
        [Arguments(StatusCodes.Status500InternalServerError)]
        public async Task OnActionExecutionAsync_WhenResponseIsNotSuccess2xx_DoesNotEvictTags(int errorStatusCode)
        {
            // Arrange 
            var attribute = new InvalidateCacheAttribute(nameof(CacheTags.DealAll));
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new ObjectResult(new { }) { StatusCode = errorStatusCode });

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).IsEmpty();
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenActionThrowsException_DoesNotEvictTags()
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute(nameof(CacheTags.InvoiceAll));
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(
                result: new ObjectResult(new { }) { StatusCode = 200 },
                exception: new InvalidOperationException("Błąd bazy danych")
            );

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).IsEmpty();
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenResultIsNotObjectResult_DoesNotEvictTags()
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute("test_tag");
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new StatusCodeResult(StatusCodes.Status200OK));

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).IsEmpty();
        }

        [Test]
        public async Task OnActionExecutionAsync_WhenTagsArrayIsEmptyOrNull_CompletesWithoutErrors()
        {
            // Arrange
            var attribute = new InvalidateCacheAttribute();
            var executingContext = CreateExecutingContext();
            var next = CreateNextDelegate(new OkObjectResult(new { }));

            // Act
            await attribute.OnActionExecutionAsync(executingContext, next);

            // Assert
            await Assert.That(_cacheStore.EvictedTags).IsEmpty();
        }
    }
}
