using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class NoteMapper
    {
        public partial NoteListCommand MapList(Guid searchId, PaggedRequest pagged, SearchRequest search);
    }
}
