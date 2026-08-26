using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface ICurrencyServices
    {
        Task<Result<List<CurrencyListResponse>>> GetCurrencySimpleListAsync();
        Task<Result<PagedResult<CurrencyListResponse>>> GetCurrenyListAsync(BasicListCommand command);
    }
}
