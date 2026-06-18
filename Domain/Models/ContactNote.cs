using Domain.Common;

namespace Domain.Models
{
    public class ContactNote : Note
    {
        public Guid ContactId { get; set; }
        public Contact Contact { get; set; } = null!;
    }
}
