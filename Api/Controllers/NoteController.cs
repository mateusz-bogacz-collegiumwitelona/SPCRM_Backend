using Api.Controllers.Base;
using Api.Mappers;
using Api.Request;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [Route("api/note")]
    [ApiController]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public class NoteController : AuthControllerBase
    {

        [EndpointSummary("Edit note")]
        [EndpointDescription("Edit an existing note.")]
        [HttpPatch("edit")]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> EditNoteAsync(
            [FromServices] INoteServices note,
            [FromServices] NoteMapper mapper,
            [FromBody] NoteEditRequest request
            )
        {
            var result = await note.EditNoteAsync(
                mapper.MapEdit(request),
                CurrentUserId
                );

            return HandleResult(result);
        }
    }
}
