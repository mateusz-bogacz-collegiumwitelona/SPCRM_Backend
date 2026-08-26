using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class DealQueryExtension
    {
        internal static IQueryable<Deal> ApplyFilter(
            this IQueryable<Deal> query,
            string? companyName,
            decimal? value,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? statusType
            )
        {
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var search = companyName.ToLower();
                query = query.Where(d => d.Company.Name.ToLower().Contains(search));
            }

            if (value.HasValue)
            {
                long dbValue = (long)(value.Value * 10000m);
                query = query.Where(d => d.Value == dbValue);
            }

            if (!string.IsNullOrWhiteSpace(statusType))
            {
                if (Enum.TryParse<DealsStatusEnum>(statusType, true, out var parsedStatus))
                {
                    query = query.Where(d => d.Status == parsedStatus);
                }
            }

            if (dateFrom.HasValue)
                query = query.Where(d => d.CloseDate >= dateFrom.Value.ToUniversalTime());

            if (dateTo.HasValue)
                query = query.Where(d => d.CloseDate <= dateTo.Value.ToUniversalTime());

            return query;
        }

        internal static IQueryable<Deal> ApplySorting(this IQueryable<Deal> query, string? sortBy, bool sortDescending)
        => sortBy?.ToLower() switch
        {
            "name" => sortDescending
                ? query.OrderByDescending(x => x.Name)
                : query.OrderBy(x => x.Name),

            "value" => sortDescending
                ? query.OrderByDescending(x => x.Value)
                : query.OrderBy(x => x.Value),

            "currency" => sortDescending
                ? query.OrderByDescending(x => x.Currency.Code)
                : query.OrderBy(x => x.Currency.Code),

            "company" => sortDescending
                ? query.OrderByDescending(x => x.Company.Name)
                : query.OrderBy(x => x.Company.Name),

            "date" => sortDescending
                ? query.OrderByDescending(x => x.CloseDate)
                : query.OrderBy(x => x.CloseDate),

            _ => query.OrderByDescending(x => x.CloseDate)
        };

        internal static IQueryable<Deal> ApplySearch(this IQueryable<Deal> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(d =>
                        EF.Functions.ILike(EF.Functions.Unaccent(d.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        (d.Company != null && EF.Functions.ILike(EF.Functions.Unaccent(d.Company.Name), EF.Functions.Unaccent(wildcardTerm))) ||
                        (d.Currency != null && EF.Functions.ILike(EF.Functions.Unaccent(d.Currency.Code), EF.Functions.Unaccent(wildcardTerm)))
                    );
                }
            }
            return query;
        }
    }
}
