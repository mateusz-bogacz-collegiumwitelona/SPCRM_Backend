using Domain.Common;
using Domain.Models;

namespace Services.Interfaces
{
    public interface IInventoryService
    {
        Task<Result> DeductStockForDealAsync(IEnumerable<DealProduct> dealProducts);
        Task<Result> ValidateStockAvailabilityAsync(Guid productId, int requestedQuantity, int currentQuantityInDeal = 0);
    }
}
