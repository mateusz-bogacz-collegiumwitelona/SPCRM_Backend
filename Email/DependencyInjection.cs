using Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEmailModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<IEmailQueue, EmailQueue>();
            services.AddScoped<IEmailSender, EmailSender>();
            services.AddHostedService<EmailBackgroundWorker>();

            return services;
        }
    }
}
