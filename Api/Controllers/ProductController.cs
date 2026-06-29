using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/products")]
    [ApiController]
    public class ProductController : AuthControllerBase
    {
        private readonly IProductSevices _productServices;

        public ProductController(IProductSevices productServices)
        {
            _productServices = productServices;
        }

        [EndpointSummary("Get product list")]
        [EndpointDescription("Get product list with pagination, sorting and filtering.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> GetProductListAsync(
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] ProductFilterRequest filter
            )
        {
            var result = await _productServices.GetProductListAsync(pagged, sorting, search, filter);

            return HandleResult(result);
        }
    }
}
