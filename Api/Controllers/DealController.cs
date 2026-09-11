using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Deal;
using Api.Request.List;
using Api.Request.Product;
using Api.Request.Sale;
using Api.Request.Task;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Deal;

namespace Api.Controllers
{
    [Route("api/sales")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class DealController : AuthControllerBase
    {
        [EndpointSummary("Get user deals")]
        [EndpointDescription("Show data of deals. Regular users only see their own deals, managers can see all or filter by OwnerId.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserDealResponse>>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetSalesAsync(
            [FromServices] IDealServices salesServices,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] DealsFilterRequest filter,
            [FromServices] DealMapper mapper
        )
        {
            Guid? forcedOwnerId = User.IsInRole("Manager") ? null : CurrentUserId;

            var command = mapper.MapList(pagged, sorting, search, filter);
            var result = await salesServices.GetDealsAsync(command, forcedOwnerId);

            return HandleResult(result);
        }

        [EndpointSummary("Get sales statuses")]
        [EndpointDescription("Show available sales statuses.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("statuses")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealsStatuses([FromServices] IDealServices salesServices)
        {
            var result = await salesServices.GetDealsStatus();
            return HandleResult(result);
        }

        [EndpointSummary("Get sale detail")]
        [EndpointDescription("Returns detailed information about a specific sale.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{dealId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealDetailAsync(
            [FromServices] IDealServices salesServices,
            [FromRoute] Guid dealId)
        {
            var result = await salesServices.GetDealDetailAsync(dealId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get deal products")]
        [EndpointDescription("Returns a paginated list of products associated with a specific deal.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{dealId}/products")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetDealProductAsync(
            [FromServices] IDealServices salesServices,
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

        [EndpointSummary("Add a new deal")]
        [EndpointDescription("Creates a new deal with the provided details.")]
        [HttpPost]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> AddDealAsync(
            [FromServices] IDealServices salesServices,
            [FromServices] DealMapper mapper,
            [FromBody] AddDealRequest request)
        {
            var result = await salesServices.AddDealAsync(mapper.MapAdd(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete a deal")]
        [EndpointDescription("Deletes a specific deal by its ID. (Changed status into Cancelled)")]
        [HttpDelete("{dealId}")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> DeleteDealAsync(
            [FromServices] IDealServices salesServices,
            [FromRoute] Guid dealId)
        {
            var result = await salesServices.DeleteDealAsync(CurrentUserId, dealId);
            return HandleResult(result);
        }

        [EndpointSummary("Extend deal close date")]
        [EndpointDescription("Extends the close date of a specific deal.")]
        [HttpPut("extend-close-date")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> ExtendDealCloseDateAsync(
            [FromServices] IDealServices salesServices,
            [FromServices] DealMapper mapper,
            [FromBody] ExtendDealCloseDateRequest request)
        {
            var result = await salesServices.ExtendDealCloseDateAsync(mapper.MapExtendCloseDate(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}

