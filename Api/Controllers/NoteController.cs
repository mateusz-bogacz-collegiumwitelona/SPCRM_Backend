using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Note;
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

        [EndpointSummary("Add note")]
        [EndpointDescription("Add note, this endpoint determinate with note type is save by NoteEnum")]
        [HttpPost]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> AddNoteAsync(
            [FromServices] INoteServices note,
            [FromServices] NoteMapper mapper,
            [FromBody] NoteAddRequest request
            )
        {
            var result = await note.AddNoteAsync(mapper.MapAdd(request, CurrentUserId));
            return HandleResult(result);
        }

        [EndpointSummary("Delete note")]
        [EndpointDescription("Delete an existing note.")]
        [HttpDelete]
        [Authorize(Roles = "Manager,User")]
        public async Task<IActionResult> DeleteNoteAsync(
            [FromServices] INoteServices note,
            [FromQuery] Guid id)
        {
            var result = await note.DeleteNoteAsync(id, CurrentUserId);
            return HandleResult(result);
        }
    }
}
