using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class MailingMapper
    {
        public partial SupportEmailCommand MapEmail(SupportEmailRequest request);
        public partial MailingCommand MapProductMailing(MailingRequest request);
    }
}
