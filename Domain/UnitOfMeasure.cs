using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class UnitOfMeasure : BaseEntity
    {
        public string Name { get; set; }
        public string Symbol { get; set; }

        public int BaseMultiplier { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
