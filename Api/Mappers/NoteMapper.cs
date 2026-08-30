using Api.Mappers.Helper;
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

        [MapProperty(nameof(NoteAddRequest.Title), nameof(NoteAddCommand.Title), Use = nameof(NormalizeTitle))]
        [MapProperty(nameof(NoteAddRequest.Content), nameof(NoteAddCommand.Content), Use = nameof(TrimContent))]
        public partial NoteAddCommand MapAdd(NoteAddRequest request, Guid authorId);

        [MapProperty(nameof(NoteEditRequest.Title), nameof(NoteEditCommand.Title), Use = nameof(NormalizeTitle))]
        [MapProperty(nameof(NoteEditRequest.Content), nameof(NoteEditCommand.Content), Use = nameof(TrimContent))]
        public partial NoteEditCommand MapEdit(NoteEditRequest request);

        private string? NormalizeTitle(string? title) => StringNormalizerHelper.NormalizeName(title);
        private string? TrimContent(string? content) => StringNormalizerHelper.Trim(content);
    }
}
