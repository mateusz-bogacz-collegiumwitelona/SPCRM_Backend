using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Workers
{
    public class PromotionCleanupWorker
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PromotionCleanupWorker> _logger;

        public PromotionCleanupWorker(AppDbContext context, ILogger<PromotionCleanupWorker> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CleanupExpiredPromotionsAsync()
        {
            _logger.LogInformation("I'm starting to clean up expired promotions...");

            var expiredPromotions = await _context.Set<Promotion>()
                .Where(p => p.IsActive && p.EndDate.HasValue && p.EndDate.Value < DateTime.UtcNow)
                .ToListAsync();

            if (expiredPromotions.Any())
            {
                foreach (var promo in expiredPromotions)
                {
                    promo.IsActive = false;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Completed. {Count} expired promotions have been disabled.", expiredPromotions.Count);
            }
            else
            {
                _logger.LogInformation("No expired promotions to disable.");
            }
        }
    }
}
