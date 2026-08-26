using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class CurrencyQueryExtension
    {
        internal static IQueryable<Currency> ApplySearch(this IQueryable<Currency> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.Like(c.Name, wildcardTerm) ||
                        EF.Functions.Like(c.Code, wildcardTerm));
                }
            }
            return query;
        }

        internal static IQueryable<Currency> ApplySorting(
            this IQueryable<Currency> query,
            string? sortBy,
            bool sortDescending)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name),
                "code" => sortDescending
                    ? query.OrderByDescending(c => c.Code)
                    : query.OrderBy(c => c.Code),

                _ => query.OrderBy(c => c.Name),
            };
        }
    }
}
