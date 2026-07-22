using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
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
            [FromServices] IContactServices contact,
            [FromServices] NoteMapper mapper,
            [FromRoute] Guid contactId,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SearchRequest search
            )
        {
            var result = await contact.GetContactNoteAsync(mapper.MapList(contactId, pagged, search));
            return HandleResult(result);
        }
    }
}
