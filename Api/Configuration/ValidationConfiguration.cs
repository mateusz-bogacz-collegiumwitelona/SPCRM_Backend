using Api.Validators.Validators.Auth;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Api.Configuration
{
    public static class ValidationConfiguration
    {
        public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
        {
            services.AddValidatorsFromAssemblyContaining<LoginValidator>();

            services.AddFluentValidationAutoValidation(configuration =>
            {
                configuration.OverrideDefaultResultFactoryWith<Api.Factory.ValidationResultFactory>();
            });

            return services;
        }
    }
}
