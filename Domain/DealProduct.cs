using Domain.Common;

namespace Domain
{
    public class DealProduct : BaseEntity
    {
        public int Quantity { get; set; }
        public long UnitPrice { get; set; } // x10000

        public Guid DealId { get; set; }
        public Deal Deal { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

    }
}
