namespace Api.Mappers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMappers(this IServiceCollection services)
        {
            services.AddSingleton<AuthMapper>();
            services.AddSingleton<CompanyMapper>();
            services.AddSingleton<ContactMapper>();
            services.AddSingleton<NoteMapper>();
            services.AddSingleton<ProductMapper>();
            services.AddSingleton<DealMapper>();
            services.AddSingleton<MailingMapper>();
            services.AddSingleton<TaskMapper>();
            services.AddSingleton<PromotionMapper>();
            services.AddSingleton<SteelGradeMapper>();
            services.AddSingleton<ApiMapper>();
            services.AddSingleton<CurrencyMapper>();
            services.AddSingleton<UnitMapper>();
            services.AddSingleton<OfferMapper>();
            services.AddSingleton<UserMapper>();
            services.AddSingleton<InvoiceMapper>();
            services.AddSingleton<AnalyticsMapper>();

            return services;
        }
    }
}
