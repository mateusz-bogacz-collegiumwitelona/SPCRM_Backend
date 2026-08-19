using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class PromotionMapper
    {
        public partial PromotionListCommand MapList(PromotionListRequest request);
        public partial ActivatePromotionCommand MapActivate(ActivatePromotionRequest request);
    }
}
