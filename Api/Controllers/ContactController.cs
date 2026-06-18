using Api.Controllers.Base;
using Domain.Common;
using DTO.Request;
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
            var result = await _contact.GetContactsAsync(pagged, filter, sorting, search);

            return HandleResult(result);
        }

        [HttpGet("companies")]
        [EndpointSummary("Get companies")]
        [EndpointDescription("Show all companies in contact list.")]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "User,Manager")]
        public async Task<IActionResult> GetCompaniesAsync()
        {
            var result = await _contact.GetCompaniesAsync();
            return HandleResult(result);
        }

        [HttpGet("{contactId}")]
        [Authorize(Roles = "User,Manager")] 
        public async Task<IActionResult> GetContactDetailAsync([FromRoute] Guid contactId)
        {
            var result = await _contact.GetContactDetailAsync(contactId);
            return HandleResult(result);
        }
    }
}
