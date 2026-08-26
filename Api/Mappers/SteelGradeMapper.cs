using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class SteelGradeMapper
    {
        public partial List<ProductReassignmentCommand>? MapReassignments(List<ProductReassignmentRequest>? reassignments);
        private partial ProductReassignmentCommand MapReassignment(ProductReassignmentRequest request);


        [MapProperty(nameof(EditSteelGradeRequest.Density), nameof(EditSteelGradeCommand.Density), Use = nameof(MapNullableDensityToDatabase))]
        public partial EditSteelGradeCommand MapEdit(EditSteelGradeRequest request);

        [MapProperty(nameof(AddSteelGradeRequest.Density), nameof(AddSteelGradeCommand.Density), Use = nameof(MapDensityToDatabase))]
        public partial AddSteelGradeCommand MapAdd(AddSteelGradeRequest request);

        private int MapDensityToDatabase(decimal density)
            => (int)Math.Round(density * 1000m);

        private int? MapNullableDensityToDatabase(decimal? density)
            => density.HasValue ? (int)Math.Round(density.Value * 1000m) : null;
    }
}
