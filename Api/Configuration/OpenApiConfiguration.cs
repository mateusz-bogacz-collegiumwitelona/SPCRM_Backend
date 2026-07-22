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

                    // Zmiana z Bearer (JWT) na ciasteczko
                    var cookieScheme = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Cookie,
                        Name = ".AspNetCore.Identity.Application",
                        Description = "Authorization via HttpOnly cookie. " +
                        "To log in, use the /login endpoint in Swagger. " +
                        "The browser will automatically store the cookie and send it with every request."
                    };

                    document.Components.SecuritySchemes.Add("CookieAuth", cookieScheme);

                    document.Security ??= new List<OpenApiSecurityRequirement>();

                    var schemeReference = new OpenApiSecuritySchemeReference("CookieAuth", document);

                    var securityRequirement = new OpenApiSecurityRequirement
                    {
                        [schemeReference] = new List<string>()
                    };

                    document.Security.Add(securityRequirement);

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
