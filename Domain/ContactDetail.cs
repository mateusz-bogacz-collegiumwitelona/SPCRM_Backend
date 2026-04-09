using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class ContactDetail : BaseEntity
    { 
        public string Type { get; set; }

        public string Value { get; set; }
        public bool IsPrimary { get; set; }

        public Guid ContactId { get; set; }
        public Contact Contact { get; set; } = null!;
    }
}
