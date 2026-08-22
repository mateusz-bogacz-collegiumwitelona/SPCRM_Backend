using Domain.Common;
using Services.Response;

namespace Services.Interfaces
{
    public interface IUnitServices
    {
        Task<Result<List<UnitListResponse>>> GetSimpleUnitList();
    }
}
