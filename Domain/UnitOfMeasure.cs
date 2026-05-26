using Domain.Common;

namespace Domain
{
    public class UnitOfMeasure : BaseEntity
    {
        public required string Name { get; set; }
        public required string Symbol { get; set; }

        public int BaseMultiplier { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
