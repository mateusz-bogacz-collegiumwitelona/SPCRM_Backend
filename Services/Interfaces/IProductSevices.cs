using Domain.Common;
using Services.Command.List;
using Services.Command.Product;
using Services.Response.Product;

namespace Services.Interfaces
{
    public interface IProductSevices
    {
        Task<Result<PagedResult<ProductResponse>>> GetProductListAsync(ProductListCommand command);
        Task<Result<IEnumerable<string>>> GetProductCategoryAsync();
        Task<Result<ProductDetailResponse>> GetProductDetailsAsync(Guid productId);
        Task<Result<PagedResult<MailingProductResponse>>> GetMailingProductsAsync(SimpleListCommand command);
        Task<Result> AddProductAsync(AddProductCommand command);
        Task<Result> EditProductAsync(EditProductCommand command);
        Task<Result<EditProductDetailResponse>> GetProductEditDetailAsync(Guid id);
        Task<Result> DeleteProductAsync(Guid id);
    }
}
