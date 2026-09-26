using Api.Attributes;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Contact;
using Api.Request.List;
using Api.Request.Task;
using Domain.Common;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Services.Interfaces;
using Services.Response.Contact;
using Services.Response.Note;
using Services.Response.Task;
using Services.Response.User;

namespace Api.Controllers
{
    [Route("api/contacts")]
    [ApiController]
    public class ContactController : BaseControlle
    {
        [EndpointSummary("Get contacts")]
        [EndpointDescription("Show all contacts.")]
        [ProducesResponseType(typeof(Result<PagedResult<ContactsResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "UserAuthPolicy", Tags = new string[] { CacheTags.ContactsList })]
        public async Task<IActionResult> GetContactsAsync(
            [FromServices] ContactMapper mapper,
            [FromServices] IContactServices contact,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] ContactFilterRequest filter,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search
            )
        {
            var result = await contact.GetContactsAsync(mapper.MapContactList(pagged, filter, sorting, search), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get companies")]
        [EndpointDescription("Show all companies in contact list.")]
        [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
        [HttpGet("companies")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactCompanies })]
        public async Task<IActionResult> GetCompaniesAsync([FromServices] IContactServices contact)
        {
            var result = await contact.GetCompaniesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get contact detail")]
        [EndpointDescription("Show detail of a specific contact.")]
        [ProducesResponseType(typeof(Result<ContactsResponse>), StatusCodes.Status200OK)]
        [HttpGet("{contactId:guid}")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactDetails })]
        public async Task<IActionResult> GetContactDetailAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId
            )
        {
            var result = await contact.GetContactDetailAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact ways")]
        [EndpointDescription("Show all ways to contact a specific contact.")]
        [ProducesResponseType(typeof(Result<List<ContactWayResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{contactId:guid}/ways")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactWays })]
        public async Task<IActionResult> GetContactWaysAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.GetContactWayAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact notes")]
        [EndpointDescription("Show all notes for a specific contact.")]
        [ProducesResponseType(typeof(Result<PagedResult<ContactNoteResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{contactId:guid}/notes")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactNotes })]
        public async Task<IActionResult> GetContactNotesAsync(
            [FromServices] INoteServices note,
            [FromServices] NoteMapper mapper,
            [FromRoute] Guid contactId,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SearchRequest search
            )
        {
            var result = await note.GetContactNoteAsync(mapper.MapList(contactId, pagged, search));
            return HandleResult(result);
        }

        [EndpointSummary("Add contact")]
        [EndpointDescription("Add a new contact.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.ContactAll), CacheTags.ClientDataForMailing, CacheTags.UserDetails)]
        public async Task<IActionResult> AddContactAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] AddContactRequest request
            )
        {
            var result = await contact.AddContactAsync(mapper.MapAdd(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact types")]
        [EndpointDescription("Show all available contact types.")]
        [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
        [HttpGet("types")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactTypes })]
        public async Task<IActionResult> GetContactTypesAsync([FromServices] IContactServices contact)
        {
            var result = await contact.GetContactTypeAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Edit contact")]
        [EndpointDescription("Edit an existing contact.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("edit")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(nameof(CacheTags.ContactAll), CacheTags.TaskContacs, CacheTags.DealDetails)]
        public async Task<IActionResult> EditContactAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] EditContactRequest request
            )
        {
            var result = await contact.EditContactAsync(mapper.MapEdit(request), CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact detail with ways")]
        [EndpointDescription("Show detail with ways for a specific contact.")]
        [ProducesResponseType(typeof(Result<ContacDetailWithWaysResponse>), StatusCodes.Status200OK)]
        [HttpGet("{contactId:guid}/detail")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactDetailWithWaysAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId
            )
        {
            var result = await contact.GetContactDetailWithWaysAsync(contactId);
            return HandleResult(result);
        }


        [EndpointSummary("Set contact as primary")]
        [EndpointDescription("Changes the specified contact to be the primary contact for their company.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("{contactId:guid}/set-primary")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(CacheTags.ContactsList, CacheTags.ContactDetails, CacheTags.ClientDataForMailing)]
        public async Task<IActionResult> SetPrimaryContactAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.SetPrimaryContactAsync(contactId, CurrentUserId);
            return HandleResult(result);
        }

        [EndpointSummary("Delete contact")]
        [EndpointDescription("Delete an existing contact.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("{contactId:guid}")]
        [Authorize(Roles = "Manager,Admin")]
        [InvalidateCache(nameof(CacheTags.ContactAll), CacheTags.UserDetails)]
        public async Task<IActionResult> DeleteContactAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.DeleteContactAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Change contact owner")]
        [EndpointDescription("Change the owner of a contact.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPatch("change-owner")]
        [Authorize(Roles = "Manager,Admin")]
        [InvalidateCache(nameof(CacheTags.ContactAll), CacheTags.UserDetails)]
        public async Task<IActionResult> ChangeContactOwnerAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] ChangeContactOwnerRequest request)
        {
            var result = await contact.ChangeContactOwnerAsync(mapper.MapChangeOwner(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get available owners")]
        [EndpointDescription("Show all available owners.")]
        [ProducesResponseType(typeof(Result<List<OwnerResponse>>), StatusCodes.Status200OK)]
        [HttpGet("available-owners")]
        [Authorize(Roles = "Manager,Admin")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.AvailableOwners })]
        public async Task<IActionResult> GetAvailableOwnersAsync([FromServices] IUserServices user)
        {
            var result = await user.GetAvailableOwnersAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get available contacts to use in create deal")]
        [EndpointDescription("Get available contacts to use in create deal")]
        [ProducesResponseType(typeof(Result<PagedResult<ContactDealResponse>>), StatusCodes.Status200OK)]
        [HttpGet("to-deals")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "GlobalAuthPolicy", Tags = new string[] { CacheTags.ContactToDeals })]
        public async Task<IActionResult> GetContactToDealAsync(
            [FromServices] IContactServices contact,
            [FromServices] ApiMapper mapper,
            [FromQuery] SimpleListRequest request
            )
        {
            var result = await contact.GetContactToDealAsync(mapper.MapSimpleList(request));
            return HandleResult(result);
        }

        [EndpointSummary("Get contact tasks")]
        [EndpointDescription("Returns a paginated list of tasks associated with a specific contact.")]
        [ProducesResponseType(typeof(Result<PagedResult<ContactTaskResponse>>), StatusCodes.Status200OK)]
        [HttpGet("{contactId:guid}/tasks")]
        [Authorize(Roles = "User,Manager")]
        [OutputCache(PolicyName = "UserAuthPolicy", Tags = new string[] { CacheTags.ContactTasks })]
        public async Task<IActionResult> GetContactTaskAsync(
            [FromServices] ITaskServices task,
            [FromServices] TaskMapper mapper,
            [FromRoute] Guid contactId,
            [FromQuery] TaskListRequest request)
        {
            var result = await task.GetContactTaskAsync(contactId, mapper.MapList(request), CurrentUserId);
            return HandleResult(result);
        }


        [EndpointSummary("Add contact tasks")]
        [EndpointDescription("Add task to a specific contact.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status201Created)]
        [HttpPost("{contactId:guid}/tasks")]
        [Authorize(Roles = "User,Manager")]
        [InvalidateCache(
            nameof(CacheTags.AnalyticsAll),
            CacheTags.ContactTasks,
            CacheTags.TasksForCalendar,
            CacheTags.UserTasks,
            CacheTags.UserDetails)]
        public async Task<IActionResult> AddContactTaskAsync(
            [FromServices] ITaskServices task,
            [FromServices] TaskMapper mapper,
            [FromRoute] Guid contactId,
            [FromBody] AddTaskRequest request
            )
        {
            var result = await task.AddTaskAsync(mapper.MapAddTaskToContact(request, contactId), CurrentUserId);
            return HandleResult(result);
        }
    }
}
