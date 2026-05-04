using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Infrastructure.Factory
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString = "User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=spcrm;";

            builder.UseNpgsql(connectionString, options =>
            {
                options.UseNetTopologySuite();
            });

            return new AppDbContext(builder.Options);
        }
    }
}
