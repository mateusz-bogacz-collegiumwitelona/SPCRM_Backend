using Domain.Common;

namespace Domain.Models
{
    public class Currency : BaseEntity
    {
        public required string Name { get; set; }
        public required string Code { get; set; }
        public int DecimalPlaces { get; set; }

        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
        public ICollection<Offer> Offers { get; set; } = new List<Offer>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
