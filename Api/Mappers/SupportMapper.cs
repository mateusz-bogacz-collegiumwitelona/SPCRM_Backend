using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SupportMapper
    {
        public partial SupportEmailCommand MapEmail(SupportEmailRequest request);
    }
}
