using Microsoft.Extensions.DependencyInjection;
using Services.Interfaces;
using Services.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<TokenServices>();
            services.AddScoped<IAuthServices, AuthServices>();
            services.AddScoped<ISupportServices, SupportServices>();
            return services;
        }
    }
}
