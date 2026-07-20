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
    public class ContactController : AuthControllerBase
    {
        private IContactServices _contact;

        public ContactController(IContactServices contact)
        {
            _contact = contact;
        }

        [EndpointSummary("Get contacts")]
        [EndpointDescription("Show all contacts.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("")]
        [Authorize(Roles = "User,Manager")]

        public async Task<IActionResult> GetContactsAsync(
            [FromQuery] PaggedRequest pagged,
            [FromQuery] ContactFilterRequest filter,
            [FromQuery] SortingRequest sorting,
            [FromQuery] SearchRequest search
            )
        {
            var mapper = new ContactMapper();
            var result = await _contact.GetContactsAsync(mapper.MapContactList(pagged, filter, sorting, search));
            return HandleResult(result);
        }

        [EndpointSummary("Get companies")]
        [EndpointDescription("Show all companies in contact list.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("companies")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetCompaniesAsync()
        {
            var result = await _contact.GetCompaniesAsync();
            return HandleResult(result);
        }

        [EndpointSummary("Get contact detail")]
        [EndpointDescription("Show detail of a specific contact.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{contactId}")]
        [Authorize(Roles = "User,Manager")] 
        public async Task<IActionResult> GetContactDetailAsync([FromRoute] Guid contactId)
        {
            var result = await _contact.GetContactDetailAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact ways")]
        [EndpointDescription("Show all ways to contact a specific contact.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{contactId}/ways")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactWaysAsync([FromRoute] Guid contactId)
        {
            var result = await _contact.GetContactWayAsync(contactId);
            return HandleResult(result);
        }

        [EndpointSummary("Get contact notes")]
        [EndpointDescription("Show all notes for a specific contact.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [HttpGet("{contactId}/notes")]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetContactNotesAsync(
            [FromRoute] Guid contactId,
            [FromQuery] PaggedRequest pagged,
            [FromQuery] SearchRequest search
            )
        {
            var mapper = new NoteMapper();
            var result = await _contact.GetContactNoteAsync(mapper.MapList(contactId, pagged, search));
            return HandleResult(result);
        }
    }
}
