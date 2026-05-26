using Domain.Common;

namespace Domain
{
    public class ProductCategory : BaseEntity
    {
        public required String Name { get; set; }
        public required String Description { get; set; }

        public ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();

    }
}
