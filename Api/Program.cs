using Api.Configuration;
using Api.Mappers;
using Api.Middlewares;
using Email;
using Infrastructure;
using Serilog;
using Services;
using Worker;

SerilogConfiguration.InitBootstrapLogger();

try
{
    Log.Information("Starting up the application...");

    var builder = WebApplication.CreateBuilder(args);

    builder.ConfigureSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();

    // Add app configuration
    builder.Services.AddCorsConfiguration(builder.Configuration);
    builder.Services.AddFluentValidationConfiguration();
    builder.Services.AddForwardedHeadersConfiguration();
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddRateLimitingConfiguration();
    builder.Services.AddRedisConfiguration(builder.Configuration);

    // Add application services
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddMappers();
    builder.Services.AddEmailModule(builder.Configuration);
    builder.Services.AddWorkerServices(builder.Configuration);

    // Auth & Error Handling
    builder.Services.AddCookieAuthentication(builder.Configuration);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();


    // Migration and seeding
    await app.ApplyMigrationsAndSeedAsync();

    // HTTP (Middleware)
    app.UseForwardedHeadersConfiguration();
    app.UseExceptionHandler();

    app.UseSwaggerUIConfiguration();

    app.UseRouting();
    app.UseCorsConfiguration();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseOutputCache();

    app.MapControllers()
        .RequireAuthorization()
        .RequireRateLimiting("per-user");

    app.UseHangfirePipeline();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
