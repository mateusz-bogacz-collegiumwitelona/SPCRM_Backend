using Api.Mappers.Helper;
using Api.Request.Currency;
using Riok.Mapperly.Abstractions;
using Services.Command.Currency;

namespace Api.Mappers
{
    [Mapper]
    public partial class CurrencyMapper : BaseMapper
    {
        [MapProperty(nameof(AddCurrencyRequest.Code), nameof(AddCurrencyCommand.Code), Use = nameof(TrimAndUpper))]
        [MapProperty(nameof(AddCurrencyRequest.Name), nameof(AddCurrencyCommand.Name), Use = nameof(NormalizeName))]
        public partial AddCurrencyCommand MapAdd(AddCurrencyRequest request);

        [MapProperty(nameof(EditCurrencyRequest.Code), nameof(EditCurrencyCommand.Code), Use = nameof(TrimAndUpper))]
        [MapProperty(nameof(EditCurrencyRequest.Name), nameof(EditCurrencyCommand.Name), Use = nameof(NormalizeNullableName))]
        public partial EditCurrencyCommand MapEdit(EditCurrencyRequest request);


    }
}
