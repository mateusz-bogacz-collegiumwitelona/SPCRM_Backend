using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Services.Command;

namespace Services.QueryExtension
{
    internal static class PromotionQueryExtension
    {
        internal static IQueryable<Promotion> ApplySearch(this IQueryable<Promotion> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(wildcardTerm))
                    );
                }
            }

            return query;
        }

        internal static IQueryable<Promotion> ApplyFilter(this IQueryable<Promotion> query, PromotionListCommand filter)
        {
            if (filter.IsActive.HasValue)
                query = query.Where(c => c.IsActive == filter.IsActive.Value);

            if (filter.FromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value, DateTimeKind.Utc);
                query = query.Where(c => !c.EndDate.HasValue || c.EndDate >= fromUtc);
            }

            if (filter.ToDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(filter.ToDate.Value, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
                query = query.Where(c => !c.StartDate.HasValue || c.StartDate <= toUtc);
            }

            if (filter.DiscountPrecentageFrom.HasValue)
                query = query.Where(c => c.DiscountPercentage >= filter.DiscountPrecentageFrom);

            if (filter.DiscountPrecentageTo.HasValue)
                query = query.Where(c => c.DiscountPercentage <= filter.DiscountPrecentageTo);

            if (filter.PromotionPriceFrom.HasValue)
                query = query.Where(c => c.PromotionalPrice >= filter.PromotionPriceFrom);

            if (filter.PromotionPriceTo.HasValue)
                query = query.Where(c => c.PromotionalPrice <= filter.PromotionPriceTo);

            return query;
        }

        internal static IQueryable<Promotion> ApplySorting(
            this IQueryable<Promotion> query,
            string? sortBy,
            bool sortDescending)
            => sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name),

                "startdate" => sortDescending
                    ? query.OrderByDescending(c => c.StartDate)
                    : query.OrderBy(c => c.StartDate),

                "enddate" => sortDescending
                    ? query.OrderByDescending(c => c.EndDate)
                    : query.OrderBy(c => c.EndDate),

                "discountpercentage" => sortDescending
                    ? query.OrderByDescending(c => c.DiscountPercentage)
                    : query.OrderBy(c => c.DiscountPercentage),

                "promotionalprice" => sortDescending
                    ? query.OrderByDescending(c => c.PromotionalPrice)
                    : query.OrderBy(c => c.PromotionalPrice),

                _ => query.OrderBy(x => x.EndDate)
            };
    }
}
