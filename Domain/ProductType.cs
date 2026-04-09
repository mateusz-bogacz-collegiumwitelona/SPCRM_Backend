using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class ProductType : BaseEntity
    {
        public String Name { get; set; }
        public String Description { get; set; }

        public Guid CategoryId { get; set; }
        public ProductCategory Category { get; set; } = null!;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
