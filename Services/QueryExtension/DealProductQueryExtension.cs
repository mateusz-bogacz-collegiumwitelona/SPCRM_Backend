using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class DealProductQueryExtension
    {
        internal static IQueryable<DealProduct> ApplySearch(this IQueryable<DealProduct> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return query;

            var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                string wildcardTerm = $"%{term}%";

                query = query.Where(dp =>
                    (dp.Product != null && (
                        EF.Functions.ILike(EF.Functions.Unaccent(dp.Product.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(dp.Product.SteelGrade.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(dp.Product.Category.ToString()), EF.Functions.Unaccent(wildcardTerm))
                    ))
                );
            }

            return query;
        }

        internal static IQueryable<DealProduct> ApplyFilter(this IQueryable<DealProduct> query, string? productCategory, string? steelGrade)
        {
            if (!string.IsNullOrWhiteSpace(productCategory))
                query = query.Where(dp => dp.Product.Category.ToString().ToLower() == productCategory.ToLower());

            if (!string.IsNullOrWhiteSpace(steelGrade))
                query = query.Where(dp => dp.Product.SteelGrade.Name.ToLower() == steelGrade.ToLower());

            return query;
        }

        internal static IQueryable<DealProduct> ApplySorting(this IQueryable<DealProduct> query, string? sortBy, bool sortDescending)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(dp => dp.Product.Name)
                    : query.OrderBy(dp => dp.Product.Name),

                "steelgrade" => sortDescending
                ? query.OrderByDescending(p => p.Product.SteelGrade.Name)
                : query.OrderBy(p => p.Product.SteelGrade.Name),

                "quantity" => sortDescending
                    ? query.OrderByDescending(dp => dp.Quantity)
                    : query.OrderBy(dp => dp.Quantity),

                "totalprice" => sortDescending
                    ? query.OrderByDescending(dp => dp.Quantity * dp.UnitPrice)
                    : query.OrderBy(dp => dp.Quantity * dp.UnitPrice),

                _ => query.OrderBy(dp => dp.Product.Name)
            };
        }
    }
}
