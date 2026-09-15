using Domain.Common;

namespace Domain.Models
{
    public class Contact : BaseEntity
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required bool IsPrimary { get; set; }
        public string? JobTitle { get; set; }

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public Guid OwnerId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<ContactDetail> ContactDetails { get; set; } = new List<ContactDetail>();
        public ICollection<ContactNote> Notes { get; set; } = new List<ContactNote>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    }
}
