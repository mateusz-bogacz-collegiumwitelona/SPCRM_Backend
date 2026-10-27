using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class CompanyAdressConfiguration : IEntityTypeConfiguration<CompanyAdress>
    {
        public void Configure(EntityTypeBuilder<CompanyAdress> builder)
        {
            builder
                .Property(sa => sa.Location)
                .HasColumnType("geometry(Point, 4326)");

            builder
                .Property(a => a.AddressType)
                .HasConversion<string>();
        }
    }
}
