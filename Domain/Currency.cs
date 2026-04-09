using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class Currency : BaseEntity
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public int DecimalPlaces { get; set; }

        public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    }
}
