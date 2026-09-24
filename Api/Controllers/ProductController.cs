using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Product;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Command.Product;
using Services.Interfaces;
using Services.Response.Product;
using Services.Response.SteelGrade;

namespace Api.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ProductController : BaseControlle
    {

        [EndpointSummary("Get product list")]
        [EndpointDescription("Get product list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<PagedResult<ProductResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductsList })]
        public async Task<IActionResult> GetProductListAsync(
            [FromServices] IProductSevices productServices,
            [FromServices] ProductMapper mapper,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] ProductFilterRequest filter
            )
        {
            var result = await productServices.GetProductListAsync(mapper.MapList(pagged, sorting, search, filter));
            return HandleResult(result);
        }

        [EndpointSummary("Get product categories")]
        [EndpointDescription("Get a list of all product categories.")]
        [ProducesResponseType(typeof(Result<IEnumerable<string>>), StatusCodes.Status200OK)]
        [HttpGet("categories")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductCategories })]
        public async Task<IActionResult> GetProductCategoryAsync([FromServices] IProductSevices productServices)
        {
            var result = await productServices.GetProductCategoryAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get product steel grades")]
        [EndpointDescription("Get a list of all product steel grades.")]
        [ProducesResponseType(typeof(Result<IEnumerable<SteelGradeResponse>>), StatusCodes.Status200OK)]
        [HttpGet("steel-grades")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductSteelGrades })]   
        public async Task<IActionResult> GetSteelGradesAsync([FromServices] ISteelGradeServices steelGradeServices)
        {
            var result = await steelGradeServices.GetSteelGradesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get product details")]
        [EndpointDescription("Get product details by product id.")]
        [ProducesResponseType(typeof(Result<ProductDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("{productId:guid}")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductDetails })]
        public async Task<IActionResult> GetProductDetailsAsync(
            [FromServices] IProductSevices productServices,
            [FromRoute] Guid productId
            )
        {
            var result = await productServices.GetProductDetailsAsync(productId);
            return HandleResult(result);
        }

        [EndpointSummary("Add product")]
        [EndpointDescription("Add a new product.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.ProductAll))]
        public async Task<IActionResult> AddProductAsync(
            [FromServices] IProductSevices product,
            [FromServices] ProductMapper mapper,
            [FromBody] AddProductRequest request
            )
        {
            var result = await product.AddProductAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }

        [EndpointSummary("Update product")]
        [EndpointDescription("Update an existing product.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("{productId:guid}")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.ProductAll), nameof(CacheTags.OffersAll), nameof(CacheTags.PromotionAll))]
        public async Task<IActionResult> UpdateProductAsync(
            [FromServices] IProductSevices product,
            [FromServices] ProductMapper mapper,
            [FromRoute] Guid productId,
            [FromBody] EditProductRequest request
            )
        {
            var result = await product.EditProductAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get product details for editing")]
        [EndpointDescription("Get product details for editing by product id.")]
        [ProducesResponseType(typeof(Result<EditProductDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("edit/{productId:guid}")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductDetails })]
        public async Task<IActionResult> GetProductEditDetailAsync(
            [FromServices] IProductSevices productServices,
            [FromRoute] Guid productId
            )
        {
            var result = await productServices.GetProductEditDetailAsync(productId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete product")]
        [EndpointDescription("Delete a product by product id.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{productId:guid}")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.ProductAll), nameof(CacheTags.OffersAll), nameof(CacheTags.PromotionAll))]
        public async Task<IActionResult> DeleteProductAsync(
            [FromServices] IProductSevices productServices,
            [FromRoute] Guid productId
            )
        {
            var result = await productServices.DeleteProductAsync(productId);
            return HandleResult(result);
        }

        [EndpointSummary("Search products for autocomplete")]
        [EndpointDescription("Returns up to 50 active products matching name or steel grade.")]
        [ProducesResponseType(typeof(Result<List<ProductAutocompleteResponse>>), StatusCodes.Status200OK)]
        [HttpGet("search")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductAutocomplete })]
        public async Task<IActionResult> SearchProductsAsync(
            [FromServices] IProductSevices productServices,
            [FromServices] ProductMapper mapper,
            [FromQuery] SearchProductAutocompleteRequest request
            )
        {
            var result = await productServices.SearchProductsAutocompleteAsync(mapper.MapSearch(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add product stock")]
        [EndpointDescription("Add stock to an existing product.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("{productId:guid}/stock")]
        [Authorize]
        [InvalidateCache(nameof(CacheTags.ProductAll))]
        public async Task<IActionResult> AddProductStockAsync(
            [FromServices] IProductSevices productServices,
            [FromServices] ProductMapper mapper,
            [FromRoute] Guid productId,
            [FromBody] AddProductStockRequest request
            )
        {
            var result = await productServices.AddProductStockAsync(mapper.MapAddStock(request, productId));
            return HandleResult(result);
        }

        [EndpointSummary("Get product deals")]
        [EndpointDescription("Get a paginated list of deals associated with a specific product. This endpoint has a search capability.")]
        [ProducesResponseType(typeof(Result<PagedResult<ProductDealItemResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{productId:guid}/deals")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductDeals })]
        public async Task<IActionResult> GetProductDealsAsync(
            [FromServices] IDealServices deal,
            [FromServices] ApiMapper mapper,
            [FromRoute] Guid productId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await deal.GetProductDealsAsync(productId, mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get product invoices")]
        [EndpointDescription("Get a paginated list of invoices associated with a specific product. This endpoint has a search capability.")]
        [ProducesResponseType(typeof(Result<PagedResult<ProductInvoiceItemResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{productId:guid}/invoices")]
        [Authorize]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ProductInvoices })]
        [ProducesResponseType(typeof(Result<PagedResult<ProductInvoiceItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProductInvoicesAsync(
            [FromServices] IInvoiceService invoice,
            [FromServices] ApiMapper mapper,
            [FromRoute] Guid productId,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await invoice.GetProductInvoicesAsync(productId, mapper.MapSimpleList(request));
            return HandleResult(result);
        }
    }
}
