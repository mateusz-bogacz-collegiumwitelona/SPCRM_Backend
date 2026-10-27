using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class SteelGradeConfiguration : IEntityTypeConfiguration<SteelGrade>
    {
        public void Configure(EntityTypeBuilder<SteelGrade> builder)
        {
            builder.HasIndex(s => s.Name)
              .IsUnique();
        }
    }
}
