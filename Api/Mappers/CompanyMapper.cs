using Api.Mappers.Helper;
using Api.Request.Company;
using Api.Request.List;
using Domain.Enum;
using NetTopologySuite.Geometries;
using Riok.Mapperly.Abstractions;
using Services.Command.Company;

namespace Api.Mappers
{
    [Mapper]
    public partial class CompanyMapper
    {
        public CompanyCommand MapBasic(Guid companyId, PaggedRequest request)
            => new CompanyCommand
            {
                CompanyId = companyId,
                PageNumber = request?.PageNumber,
                PageSize = request?.PageSize
            };

        public CompanyListCommand MapList(
            Guid userId,
            PaggedRequest pagged,
            CompanyFilterRequest filter,
            SortingRequest sorting,
            SearchRequest search)
            => new CompanyListCommand
            {
                UserId = userId,
                PageNumber = pagged?.PageNumber,
                PageSize = pagged?.PageSize,
                IsYour = filter?.IsYour,
                CreatedAtFrom = filter?.CreatedAtFrom,
                CreatedAtTo = filter?.CreatedAtTo,
                SortBy = sorting?.SortBy,
                SortDescending = sorting?.SortDescending ?? false,
                SearchTerm = search?.SearchTerm
            };

        [MapProperty(nameof(AddCompanyRequest.Name), nameof(AddCompanyCommand.Name), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddCompanyRequest.NIP), nameof(AddCompanyCommand.NIP), Use = nameof(NormalizeNip))]
        public partial AddCompanyCommand MapAdd(AddCompanyRequest request);

        public AddCompanyAdressCommand MapAddAddress(AddCompanyAdressRequest request)
            => new AddCompanyAdressCommand
            {
                Street = NormalizeName(request.Street),
                City = NormalizeName(request.City),
                ZipCode = request.ZipCode?.Trim() ?? string.Empty,
                Location = MapLocalization(request.Latitude, request.Longitude)
                           ?? throw new ArgumentException("Localization coordinates are required for new address."),
                Type = ParseAddressType(request.Type) ?? AddressTypeEnum.Branch
            };

        public partial EditCompanyCommand MapEdit(EditCompanyRequest request);

        public EditCompanyAddressCommand MapEditAddress(EditCompanyAdressRequest request)
            => new EditCompanyAddressCommand
            {
                AddressId = request.AddressId,
                Street = string.IsNullOrWhiteSpace(request.Street) ? null : NormalizeName(request.Street),
                City = string.IsNullOrWhiteSpace(request.City) ? null : NormalizeName(request.City),
                ZipCode = request.ZipCode?.Trim(),
                Location = MapLocalization(request.Latitude, request.Longitude),
                Type = ParseAddressType(request.Type)
            };

        private string NormalizeName(string? name)
            => StringNormalizerHelper.NormalizeName(name) ?? string.Empty;

        private string NormalizeNip(string? nip)
        {
            if (string.IsNullOrWhiteSpace(nip))
            {
                return string.Empty;
            }

            var clean = nip.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (clean.StartsWith("PL"))
            {
                clean = clean[2..];
            }

            return clean;
        }

        public Point? MapLocalization(float? latitude, float? longitude)
        {
            if (!latitude.HasValue || !longitude.HasValue)
            {
                return null;
            }

            return new Point(x: (double)longitude.Value, y: (double)latitude.Value)
            {
                SRID = 4326
            };
        }

        private AddressTypeEnum? ParseAddressType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;

            return Enum.TryParse<AddressTypeEnum>(type, true, out var parsedStatus)
                ? parsedStatus
                : null;
        }
    }
}
