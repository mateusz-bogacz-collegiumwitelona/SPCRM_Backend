using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Api.Configuration
{
    public static class CookieConfiguration
    {
        public static IServiceCollection AddCookieAuthentication(
            this IServiceCollection services,
            IConfiguration? configuration = null
            )
        {
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                var originalOnValidatePrincipal = options.Events.OnValidatePrincipal;
                options.Events.OnValidatePrincipal = async context =>
                {
                    await originalOnValidatePrincipal(context);

                    if (context.Principal == null
                    || context.Principal.Identity == null
                    || !context.Principal.Identity.IsAuthenticated
                    )
                    {
                        return;
                    }

                    var userIdClaim = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var isActive = await db.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);

                    if (!isActive)
                    {
                        context.RejectPrincipal();
                    }
                };
            });

            services.AddAuthentication()
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration?["JWT:ISSUER"] ?? configuration?["JWT__ISSUER"] ?? "TestIssuer",
                        ValidAudience = configuration?["JWT:AUDIENCE"] ?? configuration?["JWT__AUDIENCE"] ?? "TestAudience",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration?["JWT:KEY"] ?? configuration?["JWT__KEY"] 
                            ?? "SuperTajnyKluczTestowyOodpowiedniejDlugosci123!"
                            ))
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";
                            return System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, new
                            {
                                success = false,
                                message = "Unauthorized",
                                errors = new[] { "You are not authorized to access this resource." },
                                
                            });
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes("Identity.Application", JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            return services;
        }
    }
}
