using Domain.Common;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class NoteConfiguration : IEntityTypeConfiguration<Note>
    {
        public void Configure(EntityTypeBuilder<Note> builder)
        {
            builder
                .HasDiscriminator<string>("NoteType")
                .HasValue<ContactNote>("Contact")
                .HasValue<DealNote>("Deal")
                .HasValue<TaskNote>("Task");
        }
    }
}
