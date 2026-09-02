using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors
{
    public class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result
            )
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
            )
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ApplySoftDelete(DbContext? context)
        {
            if (context is null) return;

            var now = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries<BaseEntity>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Property(e => e.CreatedAt).CurrentValue = DateTime.SpecifyKind(now, DateTimeKind.Utc);
                        break;

                    case EntityState.Modified:
                        entry.Property(e => e.UpdateAt).CurrentValue = DateTime.SpecifyKind(now, DateTimeKind.Utc);
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Unchanged;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.UpdateAt = DateTime.SpecifyKind(now, DateTimeKind.Utc);
                        entry.State = EntityState.Modified;
                        break;
                }
            }
        }
    }
}
