using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class Offer : BaseEntity
    {
        public string Name { get; set; } = null!;
        public Guid ContactId { get; set; }
        public Contact Contact { get; set; } = null!;

        public Guid CreatedByUserId { get; set; }

        public Guid CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        public DateTime ValidUntil { get; set; }

        public OfferStatusEnum Status { get; set; }

        public ICollection<OfferProducts> Products { get; set; } = new List<OfferProducts>();
    }
}
