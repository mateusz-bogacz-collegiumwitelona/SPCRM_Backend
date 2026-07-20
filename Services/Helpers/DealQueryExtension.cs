using Domain.Enum;
using Domain.Models;

namespace Services.Helpers
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
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
                "value" => sortDescending ? query.OrderByDescending(x => x.Value) : query.OrderBy(x => x.Value),
                "currency" => sortDescending ? query.OrderByDescending(x => x.Currency) : query.OrderBy(x => x.Currency),
                "company" => sortDescending ? query.OrderByDescending(x => x.Company.Name) : query.OrderBy(x => x.Company.Name),
                "date" => sortDescending ? query.OrderByDescending(x => x.CloseDate) : query.OrderBy(x => x.CloseDate),
                _ => query.OrderByDescending(x => x.CloseDate)
            };
        }

        internal static IQueryable<Deal> ApplySearch(this IQueryable<Deal> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(d =>
                    d.Name.ToLower().Contains(searchTerm) ||
                    d.Company.Name.ToLower().Contains(searchTerm) ||
                    d.Currency.Code.ToLower().Contains(searchTerm)
                );
            }
            return query;
        }
    }
}
