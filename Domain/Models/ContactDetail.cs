using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class ContactDetail : BaseEntity
    {
        public ContactDetailTypeEnum Type { get; set; }

        public required string Value { get; set; }

        public string? Label { get; set; }

        public bool IsPrimary { get; set; }

        public Guid ContactId { get; set; }
        public Contact Contact { get; set; } = null!;
    }
}
