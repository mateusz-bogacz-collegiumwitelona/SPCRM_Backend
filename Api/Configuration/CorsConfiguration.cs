namespace Api.Configuration
{
    public static class CorsConfiguration
    {
        public const string PolicyName = "AllowSPCRMFrontend";

        public static IServiceCollection AddCorsConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var frontendUrl = configuration["FRONTEND:URL"] ?? "http://localhost:5173";

            services.AddCors(options =>
            {
                options.AddPolicy(PolicyName, policy =>
                {
                    policy.WithOrigins(frontendUrl , "http://localhost:8080", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            return services;
        }

        public static WebApplication UseCorsConfiguration(this WebApplication app)
        {
            app.UseCors(PolicyName);
            return app;
        }
    }
}
