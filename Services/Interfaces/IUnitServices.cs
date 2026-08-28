using Domain.Common;
using Services.Response.Unit;

namespace Services.Interfaces
{
    public interface IUnitServices
    {
        Task<Result<List<UnitListResponse>>> GetSimpleUnitList();
    }
}
