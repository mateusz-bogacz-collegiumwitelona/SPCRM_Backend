using Api.Mappers.Helper;
using Api.Request.Currency;
using Riok.Mapperly.Abstractions;
using Services.Command.Currency;

namespace Api.Mappers
{
    [Mapper]
    public partial class CurrencyMapper
    {
        [MapProperty(nameof(AddCurrencyRequest.Code), nameof(AddCurrencyCommand.Code), Use = nameof(NormalizeCode))]
        [MapProperty(nameof(AddCurrencyRequest.Name), nameof(AddCurrencyCommand.Name), Use = nameof(NormalizeName))]
        public partial AddCurrencyCommand MapAdd(AddCurrencyRequest request);

        [MapProperty(nameof(EditCurrencyRequest.Code), nameof(EditCurrencyCommand.Code), Use = nameof(NormalizeCode))]
        [MapProperty(nameof(EditCurrencyRequest.Name), nameof(EditCurrencyCommand.Name), Use = nameof(NormalizeName))]
        public partial EditCurrencyCommand MapEdit(EditCurrencyRequest request);

        private string? NormalizeCode(string? code) => StringNormalizerHelper.TrimAndUpper(code);
        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);
    }
}
