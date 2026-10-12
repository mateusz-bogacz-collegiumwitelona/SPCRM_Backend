using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Configuration
{
    public static class ForwardedHeadersConfiguration
    {
        public static IServiceCollection AddForwardedHeadersConfiguration(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            return services;
        }

        public static WebApplication UseForwardedHeadersConfiguration(this WebApplication app)
        {
            app.UseForwardedHeaders();
            return app;
        }
    }
}
