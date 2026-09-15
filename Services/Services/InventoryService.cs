using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(AppDbContext context, ILogger<InventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result> DeductStockForDealAsync(IEnumerable<DealProduct> dealProducts)
        {
            var productsIds = dealProducts.Select(dp => dp.ProductId).ToList();

            var proudcts = await _context.Products
                .Where(p => productsIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var dealProduct in dealProducts)
            {
                if (!proudcts.TryGetValue(dealProduct.ProductId, out var product))
                {
                    _logger.LogError($"Product with ID {dealProduct.ProductId} not found.");
                    return Result.Failure(
                        message: $"Product with ID {dealProduct.ProductId} not found.",
                        statusCode: StatusCodes.Status404NotFound,
                        errorCode: ErrorCodes.ProductNotFound);
                }

                if (product.StockQuantity < dealProduct.Quantity)
                {
                    _logger.LogWarning($"Insufficient stock for product {product.Name}. Requested: {dealProduct.Quantity}, Available: {product.StockQuantity}");
                    return Result.Failure(
                        message: $"Insufficient stock for product {product.Name}. Requested: {dealProduct.Quantity}, Available: {product.StockQuantity}",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InsufficientStock);
                }

                product.StockQuantity -= dealProduct.Quantity;
            }

            return Result.Success(
                message: "Stock deducted successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> ValidateStockAvailabilityAsync(Guid productId, int requestedQuantity, int currentQuantityInDeal = 0)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new
                {
                    p.Name,
                    p.StockQuantity,
                    ReservedQuantity = p.DealProducts
                        .Where(dp => dp.Deal.Status == DealsStatusEnum.ToDo || dp.Deal.Status == DealsStatusEnum.InProgress)
                        .Sum(dp => (int?)dp.Quantity) ?? 0
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                _logger.LogError($"Product with ID {productId} not found.");
                return Result.Failure(
                    message: $"Product with ID {productId} not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound);
            }

            var availableQuantity = product.StockQuantity - product.ReservedQuantity + currentQuantityInDeal;

            if (availableQuantity < requestedQuantity)
            {
                _logger.LogWarning("Insufficient stock for product {ProductName}. Requested: {Req}, Available: {Avail}", product.Name, requestedQuantity, availableQuantity);

                return Result.Failure(
                    message: $"Insufficient stock ({product.Name}).  Available for booking: {availableQuantity}.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation);
            }

            return Result.Success(
                message: "Sufficient stock available.",
                statusCode: StatusCodes.Status200OK);
        }
    }
}
