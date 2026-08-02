using Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;

namespace Email
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEmailModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<ISmtpEmailService, SmtpEmailService>();

            services.AddScoped<IEmailSender, EmailSender>();

            return services;
        }
    }
}
