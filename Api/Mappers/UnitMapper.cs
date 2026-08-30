using Api.Mappers.Helper;
using Api.Request.Unit;
using Riok.Mapperly.Abstractions;
using Services.Command.Unit;

namespace Api.Mappers
{
    [Mapper]
    public partial class UnitMapper
    {
        [MapProperty(nameof(AddUnitRequest.Name), nameof(AddUnitCommand.Name), Use = nameof(NormalizeName))]
        [MapProperty(nameof(AddUnitRequest.Symbol), nameof(AddUnitCommand.Symbol), Use = nameof(NormalizeSymbol))]
        public partial AddUnitCommand MapAdd(AddUnitRequest request);

        [MapProperty(nameof(EditUnitReqeust.Name), nameof(EditUnitCommand.Name), Use = nameof(NormalizeName))]
        [MapProperty(nameof(EditUnitReqeust.Symbol), nameof(EditUnitCommand.Symbol), Use = nameof(NormalizeSymbol))]
        public partial EditUnitCommand MapEdit(EditUnitReqeust request);
        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);
        private string? NormalizeSymbol(string? symbol) => StringNormalizerHelper.TrimAndLower(symbol);
    }
}
