using Domain.Common;

namespace Domain.Models
{
    public class OfferProducts : BaseEntity
    {
        public Guid OfferId { get; set; }
        public Offer Offer { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }

        public long QuotedPrice { get; set; }
        public required Currency Currency { get; set; } 
    }
}
