using Domain.Common;
using Domain.Constants;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Api.Configuration
{
    public static class RateLimitingConfiguration
    {
        public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    context.HttpContext.Response.Headers.RetryAfter = "60";

                    var response = Result.Failure(
                        message: "Request limit exceeded. Please try again later.",
                        errorCode: ErrorCodes.TooManyRequests,
                        statusCode: StatusCodes.Status429TooManyRequests
                    );

                    await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
                };

                options.AddPolicy("auth-strict", httpContext =>
                {
                    var ip = GetClientIp(httpContext);
                    return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
                });

                options.AddPolicy("anonymous", httpContext =>
                {
                    var ip = GetClientIp(httpContext);
                    return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
                });

                options.AddPolicy("per-user", httpContext =>
                {
                    var (key, multiplier) = GetUserPartitionKeyAndMultiplier(httpContext);
                    var basePermits = 100;

                    return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = basePermits * multiplier,
                        TokensPerPeriod = basePermits * multiplier,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
                });

                options.AddPolicy("expensive", httpContext =>
                {
                    var key = GetUserOrIpKey(httpContext);
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
                });

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var key = GetUserOrIpKey(httpContext);
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
                });
            });

            return services;
        }

        private static (string key, int multiplier) GetUserPartitionKeyAndMultiplier(HttpContext httpContext)
        {
            var user = httpContext.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                return (GetClientIp(httpContext), 1);
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? GetClientIp(httpContext);

            var multiplier = 1;
            if (user.IsInRole("Admin")) multiplier = 5;
            else if (user.IsInRole("Manager")) multiplier = 3;

            return (userId, multiplier);
        }

        private static string GetUserOrIpKey(HttpContext httpContext)
        {
            var user = httpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? GetClientIp(httpContext);
            }
            return GetClientIp(httpContext);
        }

        private static string GetClientIp(HttpContext httpContext)
            => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    }
}
