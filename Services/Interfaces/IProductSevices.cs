using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IProductSevices
    {
        Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(ProductListCommand command);
        Task<Result<IEnumerable<string>>> GetProductCategoryAsync();
        Task<Result<IEnumerable<string>>> GetSteelGradesAsync();
    }
}
