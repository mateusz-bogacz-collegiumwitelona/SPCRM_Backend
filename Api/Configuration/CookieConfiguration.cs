using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Strict;

                options.ExpireTimeSpan = TimeSpan.FromDays(30); 
                options.SlidingExpiration = true;

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

                    if (context.Principal?.Identity == null || !context.Principal.Identity.IsAuthenticated)
                    {
                        return;
                    }

                    var userIdClaim = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                    {
                        await RejectAndSignOutAsync(context);
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                    var now = DateTimeOffset.UtcNow;
                    var isValidUser = await db.Users.AnyAsync(u =>
                        u.Id == userId &&
                        !u.IsDeleted &&
                        (u.LockoutEnd == null || u.LockoutEnd <= now) 
                    );

                    if (!isValidUser)
                    {
                        await RejectAndSignOutAsync(context);
                    }
                };
            });

            return services;
        }

        private static async Task RejectAndSignOutAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }
}
