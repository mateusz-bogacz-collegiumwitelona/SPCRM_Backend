using Api.Mappers.Helper;
using Api.Request.SteelGrade;
using Riok.Mapperly.Abstractions;
using Services.Command.Product;
using Services.Command.SteelGrade;

namespace Api.Mappers
{
    [Mapper]
    public partial class SteelGradeMapper : BaseMapper
    {
        public partial List<ProductReassignmentCommand>? MapReassignments(List<ProductReassignmentRequest>? reassignments);
        private partial ProductReassignmentCommand MapReassignment(ProductReassignmentRequest request);

        [MapProperty(nameof(AddSteelGradeRequest.Name), nameof(AddSteelGradeCommand.Name), Use = nameof(TrimAndUpper))]

        [MapProperty(nameof(AddSteelGradeRequest.Standard), nameof(AddSteelGradeCommand.Standard), Use = nameof(TrimAndUpper))]
        [MapProperty(nameof(AddSteelGradeRequest.Density), nameof(AddSteelGradeCommand.Density), Use = nameof(MapDensity))]
        public partial AddSteelGradeCommand MapAdd(AddSteelGradeRequest request);

        [MapProperty(nameof(EditSteelGradeRequest.Name), nameof(EditSteelGradeCommand.Name), Use = nameof(TrimAndUpper))]
        [MapProperty(nameof(EditSteelGradeRequest.Standard), nameof(EditSteelGradeCommand.Standard), Use = nameof(TrimAndUpper))]
        [MapProperty(nameof(EditSteelGradeRequest.Density), nameof(EditSteelGradeCommand.Density), Use = nameof(MapNullableDensity))]
        public partial EditSteelGradeCommand MapEdit(EditSteelGradeRequest request);

        private int MapDensity(decimal density)
            => (int)Math.Round(density * 1000m);

        private int? MapNullableDensity(decimal? density)
            => density.HasValue ? (int)Math.Round(density.Value * 1000m) : null;
    }
}
