using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class InvoiceProductsConfiguration : IEntityTypeConfiguration<InvoiceProducts>
    {
        public void Configure(EntityTypeBuilder<InvoiceProducts> builder)
        {

            builder
                .HasOne(ip => ip.Invoice)
                .WithMany(i => i.InvoiceProducts)
                .HasForeignKey(ip => ip.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(ip => ip.Product)
                .WithMany()
                .HasForeignKey(ip => ip.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
