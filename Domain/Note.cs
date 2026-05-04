using Domain.Common;

namespace Domain
{
    public class Note : BaseEntity
    {
        public String Title { get; set; }
        public String Content { get; set; }

        public Guid AuthorId { get; set; }
        public ApplicationUser Author { get; set; }

        public Guid? ContactId { get; set; }
        public Contact Contact { get; set; }

        public Guid? DealId { get; set; }
        public Deal Deal { get; set; }
    }
}
