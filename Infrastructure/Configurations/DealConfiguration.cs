using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class DealConfiguration : IEntityTypeConfiguration<Deal>
    {
        public void Configure(EntityTypeBuilder<Deal> builder)
        {
            builder
                .HasOne(d => d.Owner)
                .WithMany(u => u.Deals)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .Property(d => d.Status)
                .HasConversion<string>();

            builder
               .HasOne(d => d.Contact)
               .WithMany(c => c.Deals)
               .HasForeignKey(d => d.ContactId)
               .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
