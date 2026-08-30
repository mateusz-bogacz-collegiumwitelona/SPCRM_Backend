using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class UnitQueryExtension
    {
        internal static IQueryable<UnitOfMeasure> ApplySearch(this IQueryable<UnitOfMeasure> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.Like(c.Name, wildcardTerm) ||
                        EF.Functions.Like(c.Symbol, wildcardTerm));
                }
            }
            return query;
        }

        internal static IQueryable<UnitOfMeasure> ApplySorting(
            this IQueryable<UnitOfMeasure> query,
            string? sortBy,
            bool sortDescending
            )
            => sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name),

                "symbol" => sortDescending
                    ? query.OrderByDescending(c => c.Symbol)
                    : query.OrderBy(c => c.Symbol),

                _ => query.OrderBy(c => c.Name),
            };
    }
}
