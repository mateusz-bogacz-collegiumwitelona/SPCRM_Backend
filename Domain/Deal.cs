using Domain.Common;
using Domain.Enum;

namespace Domain
{
    public class Deal : BaseEntity
    {
        public string Name { get; set; }
        public long Value { get; set; } // x10000
        public DealsStatusEnum Status { get; set; }
        public DateTime CloseDate { get; set; }

        public Guid CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        public Guid OwnerId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public ICollection<DealProduct> DealProducts { get; set; } = new List<DealProduct>();
        public ICollection<Tasks> Tasks { get; set; } = new List<Tasks>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }
}
