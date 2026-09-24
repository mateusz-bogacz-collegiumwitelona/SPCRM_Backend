using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Company;
using Api.Request.Contact;
using Api.Request.List;
using Api.Request.Sale;
using Api.Request.Task;
using Api.Request.User;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using Services.Response.Company;
using Services.Response.Contact;
using Services.Response.Deal;
using Services.Response.Task;
using Services.Response.User;

namespace Api.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : BaseControlle
    {
        [EndpointSummary("Get simple list of users")]
        [EndpointDescription("Get list of users without serach, paggination etc. And without admins")]
        [ProducesResponseType(typeof(Result<List<UserSimpleListResponse>>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UsersSimpleList })]
        [Authorize]
        public async Task<IActionResult> GetUserSimpleListAsync(
            [FromServices] IUserServices user
            )
        {
            var result = await user.GetUserSimpleListAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get list of users")]
        [EndpointDescription("Get list of users with search, paggination etc.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserListResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UsersList })]
        public async Task<IActionResult> GetUserListAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromQuery] UserListRequest request
            )
        {
            var result = await user.GetUserListAsync(mapper.MapList(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Add new user")]
        [EndpointDescription("Add new user with role")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost("create")]
        [Authorize(Roles = "Admin")]
        [EnableRateLimiting("expensive")]
        [InvalidateCache(nameof(CacheTags.UserAll), CacheTags.AnalyticsAdminMetrics)]
        public async Task<IActionResult> CreateUserAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] AddUserRequest request
            )
        {
            var result = await user.CreateUserAsync(mapper.MapAdd(request));
            return HandleResult(result);
        }

        [EndpointSummary("Confirm email")]
        [EndpointDescription("Confirm email with token")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-strict")]
        [InvalidateCache(CacheTags.UsersList, CacheTags.UserDetails)]
        public async Task<IActionResult> ConfirmEmailAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] ConfirmEmailRequest request
            )
        {
            var result = await user.ConfirmEmailAsync(mapper.MapConfirmEmail(request));
            return HandleResult(result);
        }

        [EndpointSummary("Lock out a user")]
        [EndpointDescription("Locks out a user account until a specified date or indefinitely.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("lockout")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.UserAll))]
        public async Task<IActionResult> LockoutUserAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] SetLockoutRequest request
            )
        {
            var result = await user.LockoutUserAsync(mapper.MapSetLockout(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Unlock a user")]
        [EndpointDescription("Unlocks a currently locked user account.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("{id:guid}/unlock")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(nameof(CacheTags.UserAll))]
        public async Task<IActionResult> UnlockUserAsync(
            [FromRoute] Guid id,
            [FromServices] IUserServices user
        )
        {
            var result = await user.UnlockUserAsync(id, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Soft delete a user")]
        [EndpointDescription("Soft-deletes a user account and reassigns active companies, contacts, open deals and tasks to another active user.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(
            nameof(CacheTags.UserAll),
            CacheTags.CompaniesList,
            CacheTags.ContactsList,
            CacheTags.DealsList,
            nameof(CacheTags.TaskAll),
            nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> DeleteUserAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] DeleteUserRequest request
        )
        {
            var result = await user.DeleteUserAsync(mapper.MapDelete(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Edit user details")]
        [EndpointDescription("Updates user profile information such as first name, last name, or email.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(
            nameof(CacheTags.UserAll),
            CacheTags.CompaniesList,
            CacheTags.ContactsList,
            CacheTags.DealsList,
            CacheTags.TasksForCalendar,
            CacheTags.UserTasks,
            nameof(CacheTags.AnalyticsAll))]
        public async Task<IActionResult> EditUserAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromBody] EditUserRequest request
        )
        {
            var result = await user.EditUserAsync(mapper.MapEdit(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Initiate email change")]
        [EndpointDescription("Admin initiates an email change process for a user. Sends confirmation link to new email and alert to old email.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("change-email")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeUserEmailAsync(
            [FromServices] IUserServices userService,
            [FromServices] UserMapper mapper,
            [FromBody] ChangeUserEmailRequest request
        )
        {
            var result = await userService.ChangeUserEmailAsync(mapper.MapChangeEmail(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Confirm email change")]
        [EndpointDescription("Confirms user email change using the token sent to the new email address.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPost("confirm-email-change")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-strict")]
        [InvalidateCache(nameof(CacheTags.UserAll))]
        public async Task<IActionResult> ConfirmChangeUserEmailAsync(
            [FromServices] IUserServices userService,
            [FromServices] UserMapper mapper,
            [FromBody] ConfirmChangeUserEmailRequest request
        )
        {
            var result = await userService.ConfirmChangeUserEmailAsync(mapper.MapConfirmChangeEmail(request));
            return HandleResult(result);
        }

        [EndpointSummary("Change user role")]
        [EndpointDescription("Updates user role and invalidates user active security stamp.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("role")]
        [Authorize(Roles = "Admin")]
        [InvalidateCache(
            nameof(CacheTags.UserAll), 
            nameof(CacheTags.CompanyAll),
            nameof(CacheTags.ContactAll),
            nameof(CacheTags.DealAll),
            nameof(CacheTags.TaskAll),
            nameof(CacheTags.InvoiceAll),
            nameof(CacheTags.NoteAll),
            nameof(CacheTags.OffersAll),
            nameof(CacheTags.ProductAll)
            )]
        public async Task<IActionResult> ChangeRoleAsync(
            [FromServices] IUserServices userService,
            [FromServices] UserMapper mapper,
            [FromBody] ChangeRoleRequest request
        )
        {
            var result = await userService.ChangeRoleAsync(mapper.MapChangeRole(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get user detail")]
        [EndpointDescription("Retrieves the details of a specific user.")]
        [ProducesResponseType(typeof(Result<UserDetailResponse>), StatusCodes.Status200OK)]
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UserDetails })]
        public async Task<IActionResult> GetUserDetailAsync(
            [FromServices] IUserServices userService,
            [FromRoute] Guid id
        )
        {
            var result = await userService.GetUserDetailAsync(id);
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of companies owned by user")]
        [EndpointDescription("Returns a paginated list of companies assigned to the specified user " +
            "with optional filtering, sorting, and search term.")]
        [ProducesResponseType(typeof(Result<PagedResult<CompanyResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{userId:guid}/companies")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.CompaniesList })]
        public async Task<IActionResult> GetUserCompaniesAsync(
            [FromRoute] Guid userId,
            [FromServices] CompanyMapper mapper,
            [FromServices] ICompanyServices companyServices,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] CompanyFilterRequest filter,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search)
        {
            var command = mapper.MapUserCompaniesList(
                userId,
                pagged,
                filter,
                sorting,
                search
            );

            var result = await companyServices.GetCompanyListAsync(command);
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of contacts owned by user")]
        [EndpointDescription("Returns a paginated list of contacts assigned to the specified user " +
            "with optional filtering, sorting, and search term.")]
        [ProducesResponseType(typeof(Result<PagedResult<ContactsResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{userId:guid}/contacts")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactsList })]
        public async Task<IActionResult> GetUserContactsAsync(
            [FromRoute] Guid userId,
            [FromServices] ContactMapper mapper,
            [FromServices] IContactServices contactServices,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] ContactFilterRequest filter,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search
        )
        {
            var command = mapper.MapUserContactsList(
                userId,
                pagged,
                filter,
                sorting,
                search
            );

            var result = await contactServices.GetContactsAsync(command, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of sales/deals owned by user")]
        [EndpointDescription("Returns a paginated list of deals assigned to the specified user with optional filtering, sorting, and search term.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserDealResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{userId:guid}/sales")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.DealsList })]
        public async Task<IActionResult> GetUserSalesAsync(
            [FromRoute] Guid userId,
            [FromServices] IDealServices salesServices,
            [FromServices] DealMapper mapper,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search,
            [FromQuery] DealsFilterRequest filter
        )
        {
            var command = mapper.MapList(pagged, sorting, search, filter);
            var result = await salesServices.GetDealsAsync(command, forcedOwnerId: userId);
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of tasks assigned to user")]
        [EndpointDescription("Returns a paginated list of tasks assigned to the specified user with optional filtering, sorting, and search term.")]
        [ProducesResponseType(typeof(Result<PagedResult<UserTaskResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{userId:guid}/tasks")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.UserTasks })]
        public async Task<IActionResult> GetUserTasksAsync(
            [FromRoute] Guid userId,
            [FromServices] ITaskServices taskServices,
            [FromServices] TaskMapper mapper,
            [FromQuery] TaskListRequest request
        )
        {
            var result = await taskServices.GetUserTasksAsync(mapper.MapList(request), userId);
            return HandleResult(result);
        }

        [EndpointSummary("Get list of system roles")]
        [EndpointDescription("Returns list of available system roles for filters and selects.")]
        [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
        [HttpGet("roles")]
        [Authorize(Roles = "Admin,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.RolesList })]
        public async Task<IActionResult> GetRolesAsync([FromServices] IUserServices userServices)
        {
            var result = await userServices.GetRolesAsync();
            return HandleResult(result);
        }
    }
}
