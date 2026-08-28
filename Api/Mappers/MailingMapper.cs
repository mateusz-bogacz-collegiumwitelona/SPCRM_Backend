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
        public partial SupportEmailCommand MapEmail(SupportEmailRequest request);
        public partial MailingCommand MapProductMailing(MailingRequest request);

        public partial SimpleListCommand MapSimpleList(SimpleListRequest request);
    }
}
