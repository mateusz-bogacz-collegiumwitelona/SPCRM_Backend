using Domain.Enum;
using Domain.Models;
using DTO.Request;

namespace Services.Helpers
{
    internal static class CompanyQueryExtension
    {
        internal static IQueryable<Company> ApplySearch(this IQueryable<Company> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(c =>
                    c.Name.ToLower().Contains(searchTerm) ||
                    c.NIP.Contains(searchTerm) ||
                    c.CompanyAdresses.Any(a => a.City.ToLower().Contains(searchTerm)) ||
                    c.CompanyAdresses.Any(a => a.Street.ToLower().Contains(searchTerm)) ||
                    c.CompanyAdresses.Any(a => a.ZipCode.ToLower().Contains(searchTerm)) ||
                    c.Owner.FirstName.ToLower().Contains(searchTerm) ||
                    c.Owner.LastName.ToLower().Contains(searchTerm)
                );
            }

            return query;
        }
        internal static IQueryable<Company> ApplyFiler(this IQueryable<Company> query, CompanyFilterRequest filter, Guid userId) 
        {

            if (filter.IsYour.HasValue)
            {
                if (filter.IsYour.Value)
                {
                    query = query.Where(c => c.OwnerId == userId);
                }
                else
                {
                    query = query.Where(c => c.OwnerId != userId);
                }
                    
            }

            if (filter.CreatedAtFrom.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= filter.CreatedAtFrom.Value.ToUniversalTime());
            }

            if (filter.CreatedAtTo.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= filter.CreatedAtTo.Value.ToUniversalTime());
            }

            return query;
        }

        internal static IQueryable<Company> ApplySorting(this IQueryable<Company> query, SortingRequest sorting)
        {
            return sorting.SortBy?.ToLower() switch
            {
                "name" => sorting.SortDescending
                    ? query.OrderByDescending(x => x.Name)
                    : query.OrderBy(x => x.Name),

                "nip" => sorting.SortDescending
                    ? query.OrderByDescending(x => x.NIP)
                    : query.OrderBy(x => x.NIP),

                "city" => sorting.SortDescending
                    ? query.OrderByDescending(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.City)
                        .FirstOrDefault())
                    : query.OrderBy(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.City)
                        .FirstOrDefault()),

                "zipcode" => sorting.SortDescending
                    ? query.OrderByDescending(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.ZipCode) 
                        .FirstOrDefault())
                    : query.OrderBy(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.ZipCode)
                        .FirstOrDefault()),

                "createdat" => sorting.SortDescending
                  ? query.OrderByDescending(x => x.CreatedAt)
                  : query.OrderBy(x => x.CreatedAt),

                _ => query.OrderBy(x => x.Name)
            };
        }
    }
}
