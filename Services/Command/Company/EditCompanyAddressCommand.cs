using Domain.Enum;
using NetTopologySuite.Geometries;

namespace Services.Command.Company
{
    public record EditCompanyAddressCommand
    {
        public required Guid AddressId { get; init; }
        public string? Street { get; init; }
        public string? City { get; init; }
        public string? ZipCode { get; init; }
        public Point? Location { get; init; }
        public AddressTypeEnum? Type { get; init; }
    }
}
