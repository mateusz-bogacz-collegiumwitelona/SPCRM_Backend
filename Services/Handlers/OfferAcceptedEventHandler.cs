using Domain.Enum;
using Domain.Events;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Handlers
{
    public class OfferAcceptedEventHandler : INotificationHandler<OfferAcceptedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IInventoryService _inventory;
        private readonly ILogger<OfferAcceptedEventHandler> _logger;

        public OfferAcceptedEventHandler(
            AppDbContext context,
            IInventoryService inventory,
            ILogger<OfferAcceptedEventHandler> logger
            )
        {
            _context = context;
            _inventory = inventory;
            _logger = logger;
        }

        public async Task Handle(OfferAcceptedEvent notification, CancellationToken token)
        {
            var offer = await _context.Offers
                .Include(o => o.Contact)
                .Include(o => o.Products)
                .FirstOrDefaultAsync(o => o.Id == notification.OfferId);

            if (offer == null)
            {
                _logger.LogError("Cannot create deal: Offer {OfferId} not found.", notification.OfferId);
                throw new InvalidOperationException($"Offer {notification.OfferId} not found.");
            }

            foreach (var offerProduct in offer.Products)
            {
                var stockValidation = await _inventory.ValidateStockAvailabilityAsync(offerProduct.ProductId, offerProduct.Quantity);
                if (!stockValidation.IsSuccess)
                {
                    _logger.LogWarning("Cannot accept offer {OfferId}: insufficient stock for Product {ProductId}.",
                        offer.Id, offerProduct.ProductId);

                    throw new BusinessRuleException($"The offer cannot be accepted. {stockValidation.Message}");
                }
            }

            var totalValue = offer.Products.Sum(p => (long)p.Quantity * p.QuotedPrice);

            var deal = new Deal
            {
                Name = $"SE/{offer.Name}",
                Value = totalValue,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddMonths(1),
                CurrencyId = offer.CurrencyId,
                OwnerId = offer.CreatedByUserId,
                CompanyId = offer.Contact.CompanyId,
                ContactId = offer.ContactId,
                DealProducts = offer.Products.Select(op => new DealProduct
                {
                    ProductId = op.ProductId,
                    Quantity = op.Quantity,
                    UnitPrice = op.QuotedPrice
                }).ToList()
            };

            await _context.Deals.AddAsync(deal);
            await _context.SaveChangesAsync();
        }
    }
}
