using Api.Policy;
using System.Security.Claims;

namespace Api.Configuration
{
    public static class CacheConfiguration
    {
        public static IServiceCollection AddRedisConfiguration(this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisHost = configuration["Redis:HOST"] ?? "localhost";
            var redisPort = configuration["Redis:PORT"] ?? "6379";
            var redisConnectionString = $"{redisHost}:{redisPort}";

            services.AddSingleton<AuthOutputCachePolicy>();

            services.AddOutputCache(options =>
            {
                options.AddPolicy("GlobalAuthPolicy", builder =>
                {
                    builder.AddPolicy<AuthOutputCachePolicy>();
                    builder.Expire(TimeSpan.FromHours(1));
                });

                options.AddPolicy("UserAuthPolicy", builder =>
                {
                    builder.AddPolicy<AuthOutputCachePolicy>();
                    builder.Expire(TimeSpan.FromMinutes(15));
                    builder.VaryByValue((context, ct) =>
                    {
                        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                        return ValueTask.FromResult(KeyValuePair.Create("UserId", userId));
                    });
                });
            });

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "SPCRM_";
            });

            return services;
        }
    }
}
