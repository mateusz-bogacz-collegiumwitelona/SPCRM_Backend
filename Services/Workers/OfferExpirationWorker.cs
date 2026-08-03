using Domain.Enum;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Workers
{
    public class OfferExpirationWorker
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OfferExpirationWorker> _logger;

        public OfferExpirationWorker(AppDbContext context, ILogger<OfferExpirationWorker> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ExpireOldOffersAsync()
        {
            _logger.LogInformation("Starting the process of expiring old offers...");

            var expiredOffers = await _context.Offers
                .Where(o => o.Status == OfferStatusEnum.Sent && o.ValidUntil <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredOffers.Any()) 
            {
                foreach (var offer in expiredOffers)
                {
                    offer.Status = OfferStatusEnum.Expired;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Completed. {Count} offers have been marked as Expired.", expiredOffers.Count);
            }
            else
            {
                _logger.LogInformation("No offers to expire.");
            }
        }
    }
}
