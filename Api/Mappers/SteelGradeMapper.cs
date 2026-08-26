using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SteelGradeMapper
    {
        public partial SteelGradeListCommand MapList(SteelGradeListRequest request);
        public partial List<ProductReassignmentCommand>? MapReassignments(List<ProductReassignmentRequest>? reassignments);
        private partial ProductReassignmentCommand MapReassignment(ProductReassignmentRequest request);

        [MapProperty(nameof(EditSteelGradeRequest.Density), nameof(EditSteelGradeCommand.Density), Use = nameof(MapDensityToDatabase))]
        public partial EditSteelGradeCommand MapEdit(EditSteelGradeRequest request);

        private int? MapDensityToDatabase(decimal? density)
            => density.HasValue ? (int)Math.Round(density.Value * 1000m) : null;
    }
}
