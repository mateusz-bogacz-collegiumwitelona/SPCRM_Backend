using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface ISteelGradeServices
    {
        Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync();
        Task<Result<PagedResult<SteelGradeListResponse>>> GetSteelGradeList(SteelGradeListCommand command);
    }
}
