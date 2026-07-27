using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

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
    }
}
