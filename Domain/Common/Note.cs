using Domain.Models;

namespace Domain.Common
{
    public abstract class Note : BaseEntity
    {
        public required String Title { get; set; }
        public required String Content { get; set; }

        public Guid AuthorId { get; set; }
        public required ApplicationUser Author { get; set; }
    }
}
