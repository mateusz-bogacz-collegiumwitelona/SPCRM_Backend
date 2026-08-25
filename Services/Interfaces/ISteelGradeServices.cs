using Domain.Common;
using Services.Response;

namespace Services.Interfaces
{
    public interface ISteelGradeServices
    {
        Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync();
    }
}
