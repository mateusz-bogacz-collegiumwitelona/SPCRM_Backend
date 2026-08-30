using Api.Mappers.Helper;
using Api.Request.List;
using Api.Request.Mailing;
using Api.Request.Support;
using Riok.Mapperly.Abstractions;
using Services.Command.List;
using Services.Command.Mailing;
using Services.Command.Support;

namespace Api.Mappers
{
    [Mapper]
    public partial class MailingMapper
    {
        [MapProperty(nameof(SupportEmailRequest.Email), nameof(SupportEmailCommand.Email), Use = nameof(NormalizeEmail))]
        [MapProperty(nameof(SupportEmailRequest.Title), nameof(SupportEmailCommand.Title), Use = nameof(NormalizeTitle))]
        [MapProperty(nameof(SupportEmailRequest.Message), nameof(SupportEmailCommand.Message), Use = nameof(TrimMessage))]
        public partial SupportEmailCommand MapEmail(SupportEmailRequest request);

        public partial MailingCommand MapProductMailing(MailingRequest request);

        public partial SimpleListCommand MapSimpleList(SimpleListRequest request);

        private string? NormalizeEmail(string? email) => StringNormalizerHelper.TrimAndLower(email);
        private string? NormalizeTitle(string? title) => StringNormalizerHelper.NormalizeName(title);
        private string? TrimMessage(string? message) => StringNormalizerHelper.Trim(message);
    }
}
