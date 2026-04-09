using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;

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
