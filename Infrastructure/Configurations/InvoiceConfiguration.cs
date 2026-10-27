using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder
              .HasOne(i => i.Deal)
              .WithMany(d => d.Invoices)
              .HasForeignKey(i => i.DealId)
              .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();
        }
    }
}
