using Api.Transformers;
using Microsoft.OpenApi;

namespace Api.Configuration
{
    public static class OpenApiConfiguration
    {
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                options.AddOperationTransformer<LoginRequestExamplesTransformer>();

                options.AddDocumentTransformer(static (document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();

                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                    var jwtScheme = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Wklej tutaj swój wygenerowany token JWT."
                    };

                    document.Components.SecuritySchemes.Add("Bearer", jwtScheme);
                    document.Security ??= new List<OpenApiSecurityRequirement>();

                    document.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
                    });

                    return Task.CompletedTask;
                });
            });

            return services;
        }

        public static WebApplication UseSwaggerUIConfiguration(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "SPCRM API V1");
                });
            }

            return app;
        }
    }
}
