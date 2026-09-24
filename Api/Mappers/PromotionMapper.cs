using Api.Mappers.Helper;
using Api.Request.Promotion;
using Riok.Mapperly.Abstractions;
using Services.Command.Promotion;

namespace Api.Mappers
{
    [Mapper]
    public partial class PromotionMapper
    {
        public partial PromotionListCommand MapList(PromotionListRequest request);
        public partial ActivatePromotionCommand MapActivate(ActivatePromotionRequest request);

        [MapProperty(nameof(EditPromotionRequest.Name), nameof(EditPromotionCommand.Name), Use = nameof(NormalizeName))]
        public partial EditPromotionCommand MapEdit(EditPromotionRequest request);


        [MapProperty(nameof(AddPromotionRequest.Name), nameof(AddPromotionCommand.Name), Use = nameof(NormalizeName))]
        public partial AddPromotionCommand MapAdd(AddPromotionRequest request);

        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);
    }
}
