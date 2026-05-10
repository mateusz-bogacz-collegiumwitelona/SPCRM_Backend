using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Api.Configuration
{
    public static class JwtConfiguration
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration
            )
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["JWT:ISSUER"],
                        ValidAudience = configuration["JWT:AUDIENCE"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:KEY"]!))
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
