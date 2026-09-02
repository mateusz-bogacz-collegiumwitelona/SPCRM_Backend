using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;
using Services.Services;
using Services.Workers;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<TokenServices>();
            services.AddScoped<IAuthServices, AuthServices>();
            services.AddScoped<IMailingServices, MailingServices>();
            services.AddScoped<ICompanyServices, CompanyServices>();
            services.AddScoped<ISalesServices, SalesServices>();
            services.AddScoped<IContactServices, ContactServices>();
            services.AddScoped<IDebtService, DebtService>();
            services.AddScoped<ITaskServices, TaskServices>();
            services.AddScoped<IProductSevices, ProductSevices>();
            services.AddScoped<INoteServices, NoteServices>();
            services.AddScoped<IPromotionServices, PromotionServices>();
            services.AddScoped<ICurrencyServices, CurrencyServices>();
            services.AddScoped<IUnitServices, UnitServices>();
            services.AddScoped<ISteelGradeServices, SteelGradeServices>();
            services.AddScoped<IOfferServices, OfferServices>();
            services.AddScoped<IEntityAuthorizationService, EntityAuthorizationService>();

            services.AddScoped<PromotionCleanupWorker>();
            services.AddScoped<OfferExpirationWorker>();

            services.AddHangfire(config => config
                .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));

            services.AddHangfireServer();

            return services;
        }
    }
}
