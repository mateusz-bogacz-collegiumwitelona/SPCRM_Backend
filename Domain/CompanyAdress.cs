using Domain.Common;
using Domain.Enum;
using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using System.Text;

namespace Domain
{
    public class CompanyAdress : BaseEntity
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }

        public Point? Location { get; set; }
        
        public AddressTypeEnum AddressType { get; set; }

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;
    }
}
