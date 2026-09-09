using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Company;
using Api.Request.Contact;
using Api.Request.List;
using Api.Request.User;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Company;
using Services.Response.Contact;

namespace Api.Controllers
{
    [Route("api/user")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class UserController : AuthControllerBase
    {
        [EndpointSummary("Get simple list of users")]
        [EndpointDescription("Get list of users without serach, paggination etc. And without admins")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("simple")]
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
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserListAsync(
            [FromServices] IUserServices user,
            [FromServices] UserMapper mapper,
            [FromQuery] UserListRequest request
            )
        {
            var result = await user.GetUserListAsync(mapper.MapList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Add new user")]
        [EndpointDescription("Add new user with role")]
        [HttpPost("create")]
        [Authorize(Roles = "Admin")]
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
        [HttpPost("confirm-email")]
        [AllowAnonymous]
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
        [HttpPost("lockout")]
        [Authorize(Roles = "Admin")]
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
        [HttpPost("{id:guid}/unlock")]
        [Authorize(Roles = "Admin")]
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
        [HttpDelete]
        [Authorize(Roles = "Admin")]
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
        [HttpPatch]
        [Authorize(Roles = "Admin")]
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
        [HttpPost("confirm-email-change")]
        [AllowAnonymous]
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
        [HttpPatch("role")]
        [Authorize(Roles = "Admin")]
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
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
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
        [HttpGet("{userId:guid}/companies")]
        [Authorize(Roles = "Admin")]
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
        [HttpGet("{userId:guid}/contacts")]
        [Authorize(Roles = "Admin,Manager")]
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

            var result = await contactServices.GetContactsAsync(command);
            return HandleResult(result);
        }
    }
}
