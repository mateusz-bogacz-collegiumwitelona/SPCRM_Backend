using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{

    [Route("api/steel-grade")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class SteelGradeController : AuthControllerBase
    {
        [EndpointSummary("Get steel grade list")]
        [EndpointDescription("Get steel grade list with pagination, sorting and search.")]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSteelGradeListAsync(
            [FromServices] ISteelGradeServices steelGrade,
            [FromServices] SteelGradeMapper mapper,
            [FromQuery] SteelGradeListRequest request
            )
        {
            var result = await steelGrade.GetSteelGradeListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get associated products for a steel grade")]
        [EndpointDescription("Get a list of products associated with a specific steel grade.")]
        [HttpGet("{steelGradeId:guid}/products")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAssociatedProductsAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromRoute] Guid steelGradeId)
        {
            var result = await steelGradeServices.GetAssociatedProductsAsync(steelGradeId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete a steel grade")]
        [EndpointDescription("Delete a steel grade and update related products.")]
        [HttpDelete("{steelGradeId:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromRoute] Guid steelGradeId,
            [FromBody] DeleteSteelGradeRequest? request)
        {
            var result = await steelGradeServices.DeleteSteelGradeAsync(
                steelGradeId,
                mapper.MapReassignments(request?.Reassignments)
                );
            return HandleResult(result);
        }

        [EndpointSummary("Edit a steel grade")]
        [EndpointDescription("Edit the details of an existing steel grade.")]
        [HttpPatch]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromBody] EditSteelGradeRequest request)
        {
            var result = await steelGradeServices.EditSteelGradeAsync(mapper.MapEdit(request));
            return HandleResult(result);
        }

        [EndpointSummary("Create a new steel grade")]
        [EndpointDescription("Create a new steel grade with the specified details.")]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddSteelGradeAsync(
            [FromServices] ISteelGradeServices steelGradeServices,
            [FromServices] SteelGradeMapper mapper,
            [FromBody] AddSteelGradeRequest request)
        {
            var result = await steelGradeServices.AddSteelGradeAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }
    }
}
