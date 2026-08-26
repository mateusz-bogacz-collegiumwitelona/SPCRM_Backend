using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class SteelGradeQueryExtension
    {
        internal static IQueryable<SteelGrade> ApplySeatch(this IQueryable<SteelGrade> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Standard ?? string.Empty), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Density.ToString()), EF.Functions.Unaccent(wildcardTerm))
                        );
                }
            }

            return query;
        }

        internal static IQueryable<SteelGrade> ApplySorting(
            this IQueryable<SteelGrade> query,
            string? sortBy,
            bool sortDescending)
            => sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name),

                "standard" => sortDescending
                    ? query.OrderByDescending(c => c.Standard)
                    : query.OrderBy(c => c.Standard),

                "density" => sortDescending
                    ? query.OrderByDescending(c => c.Density)
                    : query.OrderBy(c => c.Density),

                _ => query.OrderBy(c => c.Name),
            };
    }
}
