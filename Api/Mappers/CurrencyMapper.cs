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

        private string NormalizeCode(string code)
            => code.Trim().ToUpper();

        private string NormalizeName(string name)
            => name.Trim();
    }
}
