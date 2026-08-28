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

        [MapProperty(nameof(EditPromotionRequest.PromotionalPrice), nameof(EditPromotionCommand.PromotionalPrice), Use = nameof(MapPriceToCents))]
        [MapProperty(nameof(EditPromotionRequest.MinWeight), nameof(EditPromotionCommand.MinWeight), Use = nameof(MapWeightToGrams))]
        public partial EditPromotionCommand MapEdit(EditPromotionRequest request);

        [MapProperty(nameof(AddPromotionRequest.PromotionalPrice), nameof(AddPromotionCommand.PromotionalPrice), Use = nameof(MapPriceToCents))]
        [MapProperty(nameof(AddPromotionRequest.MinWeight), nameof(AddPromotionCommand.MinWeight), Use = nameof(MapWeightToGrams))]
        public partial AddPromotionCommand MapAdd(AddPromotionRequest request);

        private long? MapPriceToCents(decimal? price)
            => price.HasValue ? (long)Math.Round(price.Value * 10000m) : null;
        private int? MapWeightToGrams(decimal? weight)
            => weight.HasValue ? (int)Math.Round(weight.Value * 1000m) : null;
    }
}
