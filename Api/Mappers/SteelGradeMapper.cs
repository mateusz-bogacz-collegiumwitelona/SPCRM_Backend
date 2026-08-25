using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SteelGradeMapper
    {
        public partial SteelGradeListCommand MapList(SteelGradeListRequest request);
    }
}
