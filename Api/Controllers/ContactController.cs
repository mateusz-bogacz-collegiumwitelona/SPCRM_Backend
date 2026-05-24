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

        public async Task<IActionResult> GetContacts(
            [FromQuery] PaggedRequest pagged,
            [FromQuery] ContactFilterRequest filter,
            [FromQuery] SearchRequest search
            )
        {
            var result = await _contact.GetContacts(pagged, filter, search);

            return HandleResult(result);
        }
    }
}
