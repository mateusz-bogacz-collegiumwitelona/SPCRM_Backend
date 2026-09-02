using Domain.Enum;
using NetTopologySuite.Geometries;

namespace Services.Command.Company
{
    public record AddCompanyAdressCommand
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string ZipCode { get; init; }
        public required Point Location { get; init; }
        public required AddressTypeEnum Type { get; init; }
    }
}
