using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;
using Services.Services;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<TokenServices>();
            services.AddScoped<IAuthServices, AuthServices>();
            services.AddScoped<ISupportServices, SupportServices>();
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddScoped<ICompanyServices, CompanyServices>();
            services.AddScoped<ISalesServices, SalesServices>();
            services.AddScoped<IContactServices, ContactServices>();
            return services;
        }
    }
}
