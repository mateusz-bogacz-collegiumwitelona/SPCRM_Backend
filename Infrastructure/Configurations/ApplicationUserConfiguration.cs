using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.ToTable("User");

            builder.HasIndex(u => u.PendingEmail)
                    .IsUnique()
                    .HasFilter("\"PendingEmail\" IS NOT NULL");

        }
    }
}
