using Domain.Common;

namespace Domain
{
    public class Note : BaseEntity
    {
        public required String Title { get; set; }
        public required String Content { get; set; }

        public Guid AuthorId { get; set; }
        public required ApplicationUser Author { get; set; }

        public Guid? ContactId { get; set; }
        public required Contact Contact { get; set; }

        public Guid? DealId { get; set; }
        public required Deal Deal { get; set; }
    }
}
