using Microsoft.AspNetCore.OutputCaching;

namespace Api.Policy
{
    public sealed class AuthOutputCachePolicy : IOutputCachePolicy
    {
        public AuthOutputCachePolicy() { }

        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
        {
            var request = context.HttpContext.Request;

            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return ValueTask.CompletedTask;
            }

            context.EnableOutputCaching = true;
            context.AllowCacheLookup = true;
            context.AllowCacheStorage = true;
            context.AllowLocking = true;

            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
        {
            var response = context.HttpContext.Response;
            if (response.StatusCode == StatusCodes.Status200OK)
            {
                context.AllowCacheStorage = true;
            }
            else
            {
                context.AllowCacheStorage = false;
            }

            return ValueTask.CompletedTask;
        }
    }
}
