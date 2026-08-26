using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class ApiMapper
    {
        public partial BasicListCommand MapList(BasicListRequest request);
    }
}
