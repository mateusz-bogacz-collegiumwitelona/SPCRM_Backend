using Domain.Common.Intefaces;

namespace Domain.Common
{
    public abstract class BaseEntity : IAuditableEntity, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdateAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
