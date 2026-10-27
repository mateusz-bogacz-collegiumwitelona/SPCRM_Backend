using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ContactDetailConfiguration : IEntityTypeConfiguration<ContactDetail>
    {
        public void Configure(EntityTypeBuilder<ContactDetail> builder)
        {
            builder
                .HasOne(d => d.Contact)
                .WithMany(c => c.ContactDetails)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
             .Property(t => t.Type)
             .HasConversion<string>();

            builder
              .Property(d => d.UpdateAt)
              .IsConcurrencyToken(false);
        }
    }
}
