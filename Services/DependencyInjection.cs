using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Factory;
using Services.Factory.Interfaces;
using Services.Interfaces;
using Services.Services;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Services
            services.AddScoped<TokenServices>();
            services.AddScoped<IAuthServices, AuthServices>();
            services.AddScoped<IMailingServices, MailingServices>();
            services.AddScoped<ICompanyServices, CompanyServices>();
            services.AddScoped<IDealServices, DealServices>();
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
            services.AddScoped<IUserServices, UserServices>();

            // State Factories
            services.AddScoped<IOfferStateMachineFactory, OfferStateMachineFactory>();
            services.AddScoped<IDealStateMachineFactory, DealStateMachineFactory>();
            services.AddScoped<ITaskStateMachineFactory, TaskStateMachineFactory>();

            return services;
        }
    }
}
