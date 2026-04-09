using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Token { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdateAt { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public ICollection<Task> Tasks { get; set; } = new List<Task>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
        public ICollection<Company> Companies { get; set; } = new List<Company>();
    }
}
