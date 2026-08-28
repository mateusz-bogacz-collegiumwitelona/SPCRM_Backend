using Domain.Common;
using Services.Command.Currency;
using Services.Command.List;
using Services.Response.Currency;

namespace Services.Interfaces
{
    public interface ICurrencyServices
    {
        Task<Result<List<CurrencyListResponse>>> GetCurrencySimpleListAsync();
        Task<Result<PagedResult<CurrencyListResponse>>> GetCurrenyListAsync(BasicListCommand command);
        Task<Result> AddCurrencyAsync(AddCurrencyCommand command);
        Task<Result> EditCurrencyAsync(EditCurrencyCommand command);
    }
}
