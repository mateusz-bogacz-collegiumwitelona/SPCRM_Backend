using Domain.Common;
using Services.Response;

namespace Services.Interfaces
{
    public interface ICurrencyServices
    {
        Task<Result<List<CurrencySimpleListResponse>>> GetCurrencySimpleListAsync();
    }
}
