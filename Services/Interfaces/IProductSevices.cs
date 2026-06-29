using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IProductSevices
    {
        Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(PaggedRequest pagged,
            SortingRequest sorting,
            SearchRequest search,
            ProductFilterRequest filter
            );
    }
}
