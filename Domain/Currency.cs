using Domain.Common;

namespace Domain
{
    public class Currency : BaseEntity
    {
        public required string Name { get; set; }
        public required string Code { get; set; }
        public int DecimalPlaces { get; set; }

        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    }
}
