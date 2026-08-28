using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

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

        private string? NormalizeCode(string? code)
            => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpper();

        private string? NormalizeName(string? name)
            => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }
}
