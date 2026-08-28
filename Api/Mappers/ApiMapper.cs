using Api.Request.List;
using Riok.Mapperly.Abstractions;
using Services.Command.List;

namespace Api.Mappers
{
    [Mapper]
    public partial class ApiMapper
    {
        public partial BasicListCommand MapList(BasicListRequest request);
    }
}
