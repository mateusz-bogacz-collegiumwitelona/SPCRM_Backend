using Api.Mappers.Helper;
using Api.Request.SteelGrade;
using Riok.Mapperly.Abstractions;
using Services.Command.Product;
using Services.Command.SteelGrade;

namespace Api.Mappers
{
    [Mapper]
    public partial class SteelGradeMapper
    {
        public partial List<ProductReassignmentCommand>? MapReassignments(List<ProductReassignmentRequest>? reassignments);
        private partial ProductReassignmentCommand MapReassignment(ProductReassignmentRequest request);

        [MapProperty(nameof(AddSteelGradeRequest.Name), nameof(AddSteelGradeCommand.Name), Use = nameof(NormalizeSteelName))]
        [MapProperty(nameof(AddSteelGradeRequest.Standard), nameof(AddSteelGradeCommand.Standard), Use = nameof(NormalizeStandard))]
        [MapProperty(nameof(AddSteelGradeRequest.Density), nameof(AddSteelGradeCommand.Density), Use = nameof(MapDensityToDatabase))]
        public partial AddSteelGradeCommand MapAdd(AddSteelGradeRequest request);

        [MapProperty(nameof(EditSteelGradeRequest.Name), nameof(EditSteelGradeCommand.Name), Use = nameof(NormalizeSteelName))]
        [MapProperty(nameof(EditSteelGradeRequest.Standard), nameof(EditSteelGradeCommand.Standard), Use = nameof(NormalizeStandard))]
        [MapProperty(nameof(EditSteelGradeRequest.Density), nameof(EditSteelGradeCommand.Density), Use = nameof(MapNullableDensityToDatabase))]
        public partial EditSteelGradeCommand MapEdit(EditSteelGradeRequest request);

        private string? NormalizeSteelName(string? name) => StringNormalizerHelper.TrimAndUpper(name);
        private string? NormalizeStandard(string? standard) => StringNormalizerHelper.TrimAndUpper(standard);

        private int MapDensityToDatabase(decimal density)
            => (int)Math.Round(density * 1000m);

        private int? MapNullableDensityToDatabase(decimal? density)
            => density.HasValue ? (int)Math.Round(density.Value * 1000m) : null;
    }
}
