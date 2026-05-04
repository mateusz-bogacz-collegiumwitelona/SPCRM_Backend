using Domain.Common;

namespace Domain
{
    public class Contact : BaseEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public Guid? OwnerId { get; set; }
        public ApplicationUser Owner { get; set; }

        public ICollection<ContactDetail> ContactDetails { get; set; } = new List<ContactDetail>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
    }
}
