using Domain.Models;
using Infrastructure;
using Infrastructure.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Configuration
{
    public static class DatabaseMigrationConfiguration
    {
        public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                var dbContext = services.GetRequiredService<AppDbContext>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

                logger.LogInformation("Initiating a database migration...");
                await dbContext.Database.MigrateAsync();

                logger.LogInformation("Initiating a data seeder...");
                var seeder = new DataSeeder(roleManager, userManager, dbContext);
                await seeder.InitAsync();

                logger.LogInformation("The database has been successfully migrated and initialized.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during database migration or seeding.");
                throw;
            }
        }
    }
}
