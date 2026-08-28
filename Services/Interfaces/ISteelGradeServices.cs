using Domain.Common;
using Services.Command.List;
using Services.Command.Product;
using Services.Command.SteelGrade;
using Services.Response.Product;
using Services.Response.SteelGrade;

namespace Services.Interfaces
{
    public interface ISteelGradeServices
    {
        Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync();
        Task<Result<PagedResult<SteelGradeListResponse>>> GetSteelGradeListAsync(BasicListCommand command);
        Task<Result<List<ProductSimpleResponse>>> GetAssociatedProductsAsync(Guid steelGradeId);
        Task<Result> DeleteSteelGradeAsync(Guid id, List<ProductReassignmentCommand>? reassignments);
        Task<Result> EditSteelGradeAsync(EditSteelGradeCommand command);
        Task<Result> AddSteelGradeAsync(AddSteelGradeCommand command);
    }
}
