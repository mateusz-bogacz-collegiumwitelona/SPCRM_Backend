using Api.Configuration;
using Api.Middlewares;
using Api.Transformers;
using Domain;
using Email;
using Infrastructure;
using Infrastructure.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    String frontendUrl = builder.Configuration["FRONTEND:URL"] ?? "http://localhost:5173";
    options.AddPolicy("AllowSPCRMFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add configs
builder.Services.AddSwaggerConfiguration();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddEmailModule(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<AppDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    await dbContext.Database.MigrateAsync();

    var seeder = new DataSeeder(roleManager, userManager, dbContext);
    await seeder.InitAsync();
}

app.UseSwaggerUIConfiguration();

app.UseHttpsRedirection();
app.UseExceptionHandler();

app.UseRouting();
app.UseCors("AllowSPCRMFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();

app.Run();
