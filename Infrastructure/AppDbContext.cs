using Domain.Common;
using Domain.Common.Intefaces;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

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
        public DbSet<Tasks> Tasks { get; set; }
        public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Offer> Offers { get; set; }
        public DbSet<OfferProducts> OfferProducts { get; set; }
        public DbSet<SteelGrade> SteelGrades { get; set; }
        public DbSet<InvoiceProducts> InvoiceProducts { get; set; }
        public DbSet<InvoicePayment> InvoicePayments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            builder.HasPostgresExtension("postgis");
            builder.HasPostgresExtension("unaccent");

            ConfigureGlobalConventions(builder);
        }

        private static void ConfigureGlobalConventions(ModelBuilder builder)
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (entityType.BaseType != null) continue;

                var clrType = entityType.ClrType;

                if (typeof(IAuditableEntity).IsAssignableFrom(clrType))
                {
                    var updateAtProp = entityType.FindProperty(nameof(IAuditableEntity.UpdateAt));
                    if (updateAtProp != null)
                    {
                        updateAtProp.IsConcurrencyToken = false;
                    }
                }

                if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
                {
                    var parameter = Expression.Parameter(clrType, "e");
                    var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                    var condition = Expression.Equal(property, Expression.Constant(false));
                    var lambda = Expression.Lambda(condition, parameter);

                    builder.Entity(clrType).HasQueryFilter(lambda);
                }
            }
        }
    }
}
