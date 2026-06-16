using Domain.Common;

namespace Domain.Models
{
    public class ProductType : BaseEntity
    {
        public required String Name { get; set; }
        public required String Description { get; set; }

        public Guid CategoryId { get; set; }
        public ProductCategory Category { get; set; } = null!;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
