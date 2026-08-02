using Hangfire;
using Hangfire.Dashboard;
using Services.Workers;

namespace Api.Configuration
{
    public static class HangireConfiguration
    {
        public static WebApplication UseHangfirePipeline(this WebApplication app)
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthorizationFilter() }
            });

            RecurringJob.AddOrUpdate<PromotionCleanupWorker>(
                "cleanup-promotions-job",
                worker => worker.CleanupExpiredPromotionsAsync(),
                Cron.Daily()
            );

            return app;
        }
    }

    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                return false;
            }
            return httpContext.User.IsInRole("Admin");
        }
    }
}
