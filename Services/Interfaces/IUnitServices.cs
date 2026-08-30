using Domain.Common;
using Services.Command.List;
using Services.Command.Unit;
using Services.Response.Unit;

namespace Services.Interfaces
{
    public interface IUnitServices
    {
        Task<Result<List<UnitSimpleListResponse>>> GetSimpleUnitList();
        Task<Result<PagedResult<UnitListResponse>>> GetUnitListAsync(BasicListCommand command);
        Task<Result> AddUnitAsync(AddUnitCommand command);
    }
}
