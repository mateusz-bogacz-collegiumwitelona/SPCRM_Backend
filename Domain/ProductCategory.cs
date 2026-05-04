using Domain.Common;

namespace Domain
{
    public class ProductCategory : BaseEntity
    {
        public String Name { get; set; }
        public String Description { get; set; }

        public ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();

    }
}
