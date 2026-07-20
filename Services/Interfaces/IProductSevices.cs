using Domain.Common;
using DTO.Request;
using Services.Response;

namespace Services.Interfaces
{
    public interface IProductSevices
    {
        Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
            );
        Task<Result<IEnumerable<string>>> GetProductCategoryAsync();
        Task<Result<IEnumerable<string>>> GetSteelGradesAsync();
    }
}
