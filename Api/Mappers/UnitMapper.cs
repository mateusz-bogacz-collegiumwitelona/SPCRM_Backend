using Api.Mappers.Helper;
using Api.Request.Unit;
using Riok.Mapperly.Abstractions;
using Services.Command.Unit;

namespace Api.Mappers
{
    [Mapper]
    public partial class UnitMapper : BaseMapper
    {
        [MapProperty(nameof(AddUnitRequest.Name), nameof(AddUnitCommand.Name), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddUnitRequest.Symbol), nameof(AddUnitCommand.Symbol), Use = nameof(TrimAndLower))]
        public partial AddUnitCommand MapAdd(AddUnitRequest request);

        [MapProperty(nameof(EditUnitRequest.Name), nameof(EditUnitCommand.Name), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUnitRequest.Symbol), nameof(EditUnitCommand.Symbol), Use = nameof(TrimAndLower))]
        public partial EditUnitCommand MapEdit(EditUnitRequest request);

    }
}
