using Domain.Common;
using Services.Command.Note;
using Services.Response.Note;

namespace Services.Interfaces
{
    public interface INoteServices
    {
        Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(NoteListCommand command);
        Task<Result<List<NoteResponse>>> GetDealNotesAsync(Guid dealId);
        Task<Result<List<NoteResponse>>> GetTaskNotesAsync(Guid taskId);
        Task<Result> EditNoteAsync(NoteEditCommand command, Guid userId);
        Task<Result> AddNoteAsync(NoteAddCommand command);
        Task<Result> DeleteNoteAsync(Guid noteId, Guid userId);
    }
}
