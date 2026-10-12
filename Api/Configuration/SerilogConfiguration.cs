using Serilog;

namespace Api.Configuration
{
    public static class SerilogConfiguration
    {
        public static void InitBootstrapLogger()
        {
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Information()
               .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
               .WriteTo.Console()
               .CreateLogger();
        }

        public static void ConfigureSerilog(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog();
        }
    }
}
