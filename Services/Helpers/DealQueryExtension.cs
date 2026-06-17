using Domain.Enum;
using Domain.Models;
using DTO.Request;
using DTO.Response;

namespace Services.Helpers
{
    internal static class DealQueryExtension
    {
        internal static IQueryable<Deal> ApplyFilter(this IQueryable<Deal> query, SalesFilterRequest filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
            {
                var search = filter.CompanyName.ToLower();
                query = query.Where(d => d.Company.Name.ToLower().Contains(search));
            }

            if (filter.Value.HasValue)
            {
                long dbValue = (long)(filter.Value.Value * 10000m);
                query = query.Where(d => d.Value == dbValue);
            }

            if (!string.IsNullOrWhiteSpace(filter.StatusType))
            {
                if (!string.IsNullOrWhiteSpace(filter.StatusType))
                {
                    if (Enum.TryParse<DealsStatusEnum>(filter.StatusType, true, out var parsedStatus))
                    {
                        query = query.Where(d => d.Status == parsedStatus);
                    }
                }
            }

            if (filter.DateFrom.HasValue)
                query = query.Where(d => d.CloseDate >= filter.DateFrom.Value.ToUniversalTime());

            if (filter.DateTo.HasValue)
                query = query.Where(d => d.CloseDate <= filter.DateTo.Value.ToUniversalTime());

            return query;
        }

        internal static IQueryable<UserSalesResponse> ApplySorting(this IQueryable<UserSalesResponse> query, SortingRequest request)
        {
            return request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
                "value" => request.SortDescending ? query.OrderByDescending(x => x.Value) : query.OrderBy(x => x.Value),
                "currency" => request.SortDescending ? query.OrderByDescending(x => x.Currency) : query.OrderBy(x => x.Currency),
                "company" => request.SortDescending ? query.OrderByDescending(x => x.CompanyName) : query.OrderBy(x => x.CompanyName),
                "date" => request.SortDescending ? query.OrderByDescending(x => x.CloseDate) : query.OrderBy(x => x.CloseDate),
                _ => query.OrderByDescending(x => x.CloseDate)
            };
        }

        internal static IQueryable<UserSalesResponse> ApplySearch(this IQueryable<UserSalesResponse> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(d =>
                    d.Name.ToLower().Contains(searchTerm) ||
                    d.CompanyName.ToLower().Contains(searchTerm) ||
                    d.Currency.ToLower().Contains(searchTerm)
                );
            }
            return query;
        }
    }
}
