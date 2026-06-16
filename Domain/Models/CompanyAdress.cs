using Domain.Common;
using Domain.Enum;
using NetTopologySuite.Geometries;

namespace Domain.Models
{
    public class CompanyAdress : BaseEntity
    {
        public required string Street { get; set; }
        public required string City { get; set; }
        public required string ZipCode { get; set; }

        public Point? Location { get; set; }

        public AddressTypeEnum AddressType { get; set; }

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;
    }
}
