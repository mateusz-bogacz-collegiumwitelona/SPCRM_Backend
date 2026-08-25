using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class ProductQueryExtension
    {
        internal static IQueryable<Product> ApplySearch(this IQueryable<Product> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return query;

            var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                string wildcardTerm = $"%{term}%";

                query = query.Where(p =>
                    EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                    EF.Functions.ILike(EF.Functions.Unaccent(p.SteelGrade.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                    EF.Functions.ILike(EF.Functions.Unaccent(p.Category.ToString()), EF.Functions.Unaccent(wildcardTerm))
                );
            }

            return query;
        }

        internal static IQueryable<Product> ApplyFilter(
            this IQueryable<Product> query,
            string? productCategory,
            string? steelGrade,
            bool? hasActivePromotion = null
            )
        {
            if (!string.IsNullOrWhiteSpace(productCategory) &&
                Enum.TryParse<ProductCategoryEnum>(productCategory, true, out var categoryEnum))
            {
                query = query.Where(p => p.Category == categoryEnum);
            }

            if (!string.IsNullOrWhiteSpace(steelGrade))
                query = query.Where(p => p.SteelGrade.Name.ToLower() == steelGrade.ToLower());

            if (hasActivePromotion == true)
            {
                var now = DateTime.UtcNow;
                query = query.Where(p => p.Promotions.Any(pr =>
                    pr.IsActive &&
                    (!pr.StartDate.HasValue || pr.StartDate <= now) &&
                    (!pr.EndDate.HasValue || pr.EndDate >= now)));
            }

            return query;
        }

        internal static IQueryable<Product> ApplySorting(this IQueryable<Product> query, string? sortBy, bool sortDescending)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),

                "steelgrade" => sortDescending
                    ? query.OrderByDescending(p => p.SteelGrade.Name)
                    : query.OrderBy(p => p.SteelGrade.Name),

                "quantity" => sortDescending
                    ? query.OrderByDescending(p => p.StockQuantity)
                    : query.OrderBy(p => p.StockQuantity),

                _ => query.OrderBy(p => p.Name)
            };
        }
    }
}
