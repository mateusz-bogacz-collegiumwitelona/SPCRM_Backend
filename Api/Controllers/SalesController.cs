using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Product;
using Api.Request.Sale;
using Api.Request.Task;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Sale;

namespace Api.Controllers
{
    [Route("api/sales")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class SalesController : AuthControllerBase
    {
        [EndpointSummary("Get user deals")]
        [EndpointDescription("Show data of deals. Regular users only see their own deals, managers can see all or filter by OwnerId.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserSalesResponse>>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSalesAsync(
            [FromServices] ISalesServices salesServices,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] SalesFilterRequest filter,
            [FromServices] SalesMapper mapper
        )
        {
            Guid? forcedOwnerId = User.IsInRole("Manager") ? null : CurrentUserId;

            var command = mapper.MapList(pagged, sorting, search, filter);
            var result = await salesServices.GetSalesAsync(command, forcedOwnerId);

            return HandleResult(result);
        }

        [EndpointSummary("Get sales statuses")]
        [EndpointDescription("Show available sales statuses.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("statuses")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSalesStatuses([FromServices] ISalesServices salesServices)
        {
            var result = await salesServices.GetSalesStatus();
            return HandleResult(result);
        }

        [EndpointSummary("Get sale detail")]
        [EndpointDescription("Returns detailed information about a specific sale.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{dealId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSaleDetailAsync(
            [FromServices] ISalesServices salesServices,
            [FromRoute] Guid dealId)
        {
            var result = await salesServices.GetSaleDetailAsync(dealId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get deal products")]
        [EndpointDescription("Returns a paginated list of products associated with a specific deal.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{dealId}/products")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealProductAsync(
            [FromServices] ISalesServices salesServices,
            [FromServices] ProductMapper mapper,
            [FromRoute] Guid dealId,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] ProductFilterRequest filter)
        {
            var command = mapper.MapList(pagged, sorting, search, filter);
            var result = await salesServices.GetDealProductAsync(dealId, command, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get deal notes")]
        [EndpointDescription("Returns a list of notes associated with a specific deal.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{dealId}/notes")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealNotesAsync(
            [FromServices] INoteServices note,
            [FromRoute] Guid dealId)
        {
            var result = await note.GetDealNotesAsync(dealId);
            return HandleResult(result);
        }

        [EndpointSummary("Get deal tasks")]
        [EndpointDescription("Returns a paginated list of tasks associated with a specific deal.")]
        [HttpGet("{dealId}/tasks")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealTasksAsync(
            [FromServices] ITaskServices task,
            [FromServices] TaskMapper mapper,
            [FromRoute] Guid dealId,
            [FromQuery] SalesTaskListRequest request
            )
        {
            var result = await task.GetDealTasksAsync(dealId, mapper.MapSaleTasks(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}

