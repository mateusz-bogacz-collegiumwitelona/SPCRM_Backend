using Domain.Common;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response;

namespace Services.Services
{
    public class PromotionServices : IPromotionServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PromotionServices> _logger;

        public PromotionServices(AppDbContext context, ILogger<PromotionServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<PagedResult<PromotionResponse>>> GetPromotionListAsync(PromotionListCommand command)
        {
            var query = _context.Promotions
                .AsNoTracking()
                .ApplyFilter(command)
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(p => new PromotionResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    DiscountPercentage = p.DiscountPercentage,
                    PromotionalPrice = p.PromotionalPrice,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "promotions");
        }
    }
}
