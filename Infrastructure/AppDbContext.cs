using Domain.Common;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<CompanyAdress> CompanyAdresses { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ContactDetail> ContactDetails { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<DealProduct> DealProducts { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Tasks> Tasks { get; set; }
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasPostgresExtension("postgis");

            builder.Entity<CompanyAdress>()
                .Property(sa => sa.Location)
                .HasColumnType("geometry(Point, 4326)");

            builder.Entity<ApplicationUser>()
                .ToTable("User");

            builder.Entity<Deal>()
                .HasOne(d => d.Owner)
                .WithMany(u => u.Deals)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Tasks>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Company>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Contact>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Deal>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Tasks>().HasQueryFilter(x => !x.IsDeleted);
            builder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);

            builder.Entity<CompanyAdress>()
                .Property(a => a.AddressType)
                .HasConversion<string>();

            builder.Entity<Deal>()
                .Property(d => d.Status)
                .HasConversion<string>();

            builder.Entity<Tasks>()
                .Property(t => t.Priority)
                .HasConversion<string>();

            builder.Entity<Tasks>()
                .Property(t => t.Status)
                .HasConversion<string>();

            builder.Entity<ContactDetail>()
                .Property(t => t.Type)
                .HasConversion<string>();

            builder.Entity<Note>()
                .HasDiscriminator<string>("NoteType")
                .HasValue<ContactNote>("Contact")
                .HasValue<DealNote>("Deal")
                .HasValue<TaskNote>("Task");
        }
    }
}
