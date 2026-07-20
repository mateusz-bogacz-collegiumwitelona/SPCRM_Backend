using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class NoteMapper
    {
        [MapProperty(nameof(searchId), nameof(NoteListCommand.searchId))]
        public partial NoteListCommand MapList(Guid searchId, PaggedRequest pagged, SearchRequest search);
    }
}
