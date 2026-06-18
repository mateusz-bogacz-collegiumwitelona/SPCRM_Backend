using Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Domain.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdateAt { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}
