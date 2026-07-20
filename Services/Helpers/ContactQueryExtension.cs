using Domain.Models;

namespace Services.Helpers
{
    internal static class ContactQueryExtension
    {
        internal static IQueryable<Contact> ApplySorting(this IQueryable<Contact> query, string? sortBy, bool sortDescending)
        {
            return sortBy?.ToLower() switch 
            {
                "firstname" => sortDescending ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName),
                "lastname" => sortDescending ? query.OrderByDescending(x => x.LastName) : query.OrderBy(x => x.LastName),
                "companyname" => sortDescending ? query.OrderByDescending(x => x.Company.Name) : query.OrderBy(x => x.Company.Name),
                _ => query.OrderByDescending(x => x.Company.Name)
            };
        }

        internal static IQueryable<Contact> ApplyFilter(this IQueryable<Contact> query, string? companyName, bool? isPrimary)
        {

            if (!string.IsNullOrEmpty(companyName))
                query = query.Where(c => c.Company.Name.ToLower().Contains(companyName.ToLower()));

            if (isPrimary.HasValue)
                query = query.Where(c => c.IsPrimary == isPrimary.Value);

            return query;
        }

        internal static IQueryable<Contact> ApplySearch(this IQueryable<Contact> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(c =>
                    c.FirstName.ToLower().Contains(searchTerm) ||
                    c.LastName.ToLower().Contains(searchTerm) ||
                    c.Company.Name.ToLower().Contains(searchTerm) ||
                    c.Owner.FirstName.ToLower().Contains(searchTerm) ||
                    c.Owner.LastName.ToLower().Contains(searchTerm)
                    );
            }
            return query;
        }
     }
}
