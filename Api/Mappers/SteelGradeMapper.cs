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
    }
}
