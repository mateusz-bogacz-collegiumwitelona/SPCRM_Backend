namespace Api.Mappers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMappers(this IServiceCollection services)
        {
            services.AddScoped<AuthMapper>();
            services.AddScoped<CompanyMapper>();
            services.AddScoped<ContactMapper>();
            services.AddScoped<NoteMapper>();
            services.AddScoped<ProductMapper>();
            services.AddScoped<SalesMapper>();
            services.AddScoped<MailingMapper>();
            services.AddScoped<TaskMapper>();
            services.AddScoped<PromotionMapper>();

            return services;
        }
    }
}
