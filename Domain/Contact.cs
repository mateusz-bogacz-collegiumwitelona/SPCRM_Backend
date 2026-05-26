using Domain.Common;

namespace Domain
{
    public class Contact : BaseEntity
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public Guid? OwnerId { get; set; }
        public required ApplicationUser Owner { get; set; }

        public ICollection<ContactDetail> ContactDetails { get; set; } = new List<ContactDetail>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
    }
}
