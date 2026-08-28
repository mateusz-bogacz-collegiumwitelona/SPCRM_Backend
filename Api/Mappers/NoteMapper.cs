using Api.Request.List;
using Api.Request.Note;
using Riok.Mapperly.Abstractions;
using Services.Command.Note;

namespace Api.Mappers
{
    [Mapper]
    public partial class NoteMapper
    {
        public NoteListCommand MapList(
            Guid searchId,
            PaggedRequest pagged,
            SearchRequest search)
            => new NoteListCommand
            {
                SearchId = searchId,
                PageNumber = pagged?.PageNumber,
                PageSize = pagged?.PageSize,
                SearchTerm = search?.SearchTerm
            };

        public partial NoteEditCommand MapEdit(NoteEditRequest request);
        public partial NoteAddCommand MapAdd(NoteAddRequest request, Guid authorId);
    }
}
