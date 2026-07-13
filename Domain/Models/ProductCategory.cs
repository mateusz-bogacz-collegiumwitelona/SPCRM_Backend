using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class ProductCategory : BaseEntity
    {
        public required String Name { get; set; }
        public required String Description { get; set; }
        public ProductCategoryEnum Category { get; set; }
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
