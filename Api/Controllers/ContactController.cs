using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Contact;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/contacts")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class ContactController : AuthControllerBase
    {
        [EndpointSummary("Get contacts")]
        [EndpointDescription("Show all contacts.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactsAsync(
            [FromServices] ContactMapper mapper,
            [FromServices] IContactServices contact,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] ContactFilterRequest filter,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search
            )
        {
            var result = await contact.GetContactsAsync(mapper.MapContactList(pagged, filter, sorting, search));
            return HandleResult(result);
        }

        [EndpointSummary("Get companies")]
        [EndpointDescription("Show all companies in contact list.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("companies")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetCompaniesAsync([FromServices] IContactServices contact)
        {
            var result = await contact.GetCompaniesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get contact detail")]
        [EndpointDescription("Show detail of a specific contact.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [HttpGet("{contactId}")]
        [Authorize(Roles = "User,Manager")]
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
        [HttpGet("{contactId}/ways")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactWaysAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.GetContactWayAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact notes")]
        [EndpointDescription("Show all notes for a specific contact.")]
        [HttpGet("{contactId}/notes")]
        [Authorize(Roles = "User,Manager")]
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

        [HttpPost]
        [EndpointSummary("Add contact")]
        [EndpointDescription("Add a new contact.")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> AddContactAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] AddContactRequest request
            )
        {
            var result = await contact.AddContactAsync(mapper.MapAdd(request), CurrentUserId);
            return HandleResult(result);
        }

        [HttpGet("types")]
        [EndpointSummary("Get contact types")]
        [EndpointDescription("Show all available contact types.")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactTypesAsync([FromServices] IContactServices contact)
        {
            var result = await contact.GetContactTypeAsync();
            return HandleResult(result);
        }

        [HttpPatch("edit")]
        [EndpointSummary("Edit contact")]
        [EndpointDescription("Edit an existing contact.")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> EditContactAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] EditContactRequest request
            )
        {
            var result = await contact.EditContactAsync(mapper.MapEdit(request), CurrentUserId);
            return HandleResult(result);
        }

        [HttpGet("{contactId}/detail")]
        [EndpointSummary("Get contact detail command")]
        [EndpointDescription("Show detail command for a specific contact.")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactDetailCommandAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId
            )
        {
            var result = await contact.GetContactDetailCommand(contactId);
            return HandleResult(result);
        }

        [HttpPatch("{contactId}/set-primary")]
        [EndpointSummary("Set contact as primary")]
        [EndpointDescription("Changes the specified contact to be the primary contact for their company.")]
        public async Task<IActionResult> SetPrimaryContactAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.SetPrimaryContactAsync(contactId, CurrentUserId);
            return HandleResult(result);
        }

        [HttpDelete("{contactId}")]
        [EndpointSummary("Delete contact")]
        [EndpointDescription("Delete an existing contact.")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> DeleteContactAsync(
            [FromServices] IContactServices contact,
            [FromRoute] Guid contactId)
        {
            var result = await contact.DeleteContactAsync(contactId);
            return HandleResult(result);
        }

        [HttpPatch("change-owner")]
        [EndpointSummary("Change contact owner")]
        [EndpointDescription("Change the owner of a contact.")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> ChangeContactOwnerAsync(
            [FromServices] IContactServices contact,
            [FromServices] ContactMapper mapper,
            [FromBody] ChangeContactOwnerRequest request)
        {
            var result = await contact.ChangeContactOwnerAsync(mapper.MapChangeOwner(request));
            return HandleResult(result);
        }

        [HttpGet("available-owners")]
        [EndpointSummary("Get available owners")]
        [EndpointDescription("Show all available owners.")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAvailableOwnersAsync([FromServices] IUserServices user)
        {
            var result = await user.GetAvailableOwnersAsync();
            return HandleResult(result);
        }
    }
}
