using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/products")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class ProductController : AuthControllerBase
    {

        [EndpointSummary("Get product list")]
        [EndpointDescription("Get product list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("categories")]
        [Authorize]
        public async Task<IActionResult> GetProductCategoryAsync([FromServices] IProductSevices productServices)
        {
            var result = await productServices.GetProductCategoryAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get product steel grades")]
        [EndpointDescription("Get a list of all product steel grades.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("steel-grades")]
        [Authorize]
        public async Task<IActionResult> GetSteelGradesAsync([FromServices] ISteelGradeServices steelGradeServices)
        {
            var result = await steelGradeServices.GetSteelGradesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get product details")]
        [EndpointDescription("Get product details by product id.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{productId:guid}")]
        [Authorize]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpPut("{productId:guid}")]
        [Authorize(Roles = "Admin")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("edit/{productId:guid}")]
        [Authorize(Roles = "Admin")]
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
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpDelete("{productId:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProductAsync(
            [FromServices] IProductSevices productServices,
            [FromRoute] Guid productId
            )
        {
            var result = await productServices.DeleteProductAsync(productId);
            return HandleResult(result);
        }
    }
}
