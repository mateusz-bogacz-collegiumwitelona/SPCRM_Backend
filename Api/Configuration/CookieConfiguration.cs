using Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

            return services;
        }
    }
}
