using Email.Interfaces;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Worker.Workers;

namespace Worker
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Workers
            services.AddScoped<PromotionCleanupWorker>();
            services.AddScoped<OfferExpirationWorker>();

            services.AddScoped<IInvoiceEmailWorker, InvoiceEmailWorker>();

            // Hangfire
            services.AddHangfire(config => config
                .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));

            services.AddHangfireServer();
            return services;
        }
    }
}
