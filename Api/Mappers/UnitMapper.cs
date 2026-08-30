using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command.Unit;

namespace Api.Mappers
{
    [Mapper]
    public partial class UnitMapper
    {
        public partial AddUnitCommand MapAdd(AddUnitRequest request);
    }
}
