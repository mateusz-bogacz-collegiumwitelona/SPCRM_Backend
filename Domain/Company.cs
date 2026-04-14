using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class Company : BaseEntity
    {
        public String Name { get; set; }
        public String NIP { get; set; }

        public Guid OwnerId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public ICollection<CompanyAdress> CompanyAdresses { get; set; } = new List<CompanyAdress>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
    }
}
