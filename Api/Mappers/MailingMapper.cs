using Api.Mappers.Helper;
using Api.Request.Mailing;
using Api.Request.Support;
using Riok.Mapperly.Abstractions;
using Services.Command.Mailing;
using Services.Command.Support;

namespace Api.Mappers
{
    [Mapper]
    public partial class MailingMapper : BaseMapper
    {
        [MapProperty(nameof(SupportEmailRequest.Email), nameof(SupportEmailCommand.Email), Use = nameof(NormalizeEmail))]
        [MapProperty(nameof(SupportEmailRequest.Title), nameof(SupportEmailCommand.Title), Use = nameof(NormalizeName))]
        [MapProperty(nameof(SupportEmailRequest.Message), nameof(SupportEmailCommand.Message), Use = nameof(Trim))]
        public partial SupportEmailCommand MapEmail(SupportEmailRequest request);

        public partial MailingCommand MapProductMailing(MailingRequest request);
    }
}
