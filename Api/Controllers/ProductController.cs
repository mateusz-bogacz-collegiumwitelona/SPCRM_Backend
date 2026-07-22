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
        [Authorize(Roles = "Manager,User")]
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
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetProductCategoryAsync([FromServices] IProductSevices productServices)
        {
            var result = await productServices.GetProductCategoryAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get product steel grades")]
        [EndpointDescription("Get a list of all product steel grades.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("steel-grades")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetSteelGradesAsync([FromServices] IProductSevices productServices)
        {
            var result = await productServices.GetSteelGradesAsync();
            return HandleResult(result);
        }
    }
}
