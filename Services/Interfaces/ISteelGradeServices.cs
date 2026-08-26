using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface ISteelGradeServices
    {
        Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync();
        Task<Result<PagedResult<SteelGradeListResponse>>> GetSteelGradeListAsync(SteelGradeListCommand command);
        Task<Result<List<ProductSimpleResponse>>> GetAssociatedProductsAsync(Guid steelGradeId);
        Task<Result> DeleteSteelGradeAsync(Guid id, List<ProductReassignmentCommand>? reassignments);
        Task<Result> EditSteelGradeAsync(EditSteelGradeCommand command);
    }
}
