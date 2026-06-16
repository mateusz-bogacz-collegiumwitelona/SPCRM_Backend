using Domain.Common;

namespace Domain.Models
{
    public class Company : BaseEntity
    {
        public required String Name { get; set; }
        public required String NIP { get; set; }

        public Guid OwnerId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public ICollection<CompanyAdress> CompanyAdresses { get; set; } = new List<CompanyAdress>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
