using Domain.Models;
using DTO.Request;
using DTO.Response;


namespace Services.Helpers
{
    internal static class ContactQueryExtension
    {
        internal static IQueryable<ContactsResponse> ApplySorting(this IQueryable<ContactsResponse> query, SortingRequest request)
        {
            return request.SortBy?.ToLower() switch 
            {
                "firstname" => request.SortDescending ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName),
                "lastname" => request.SortDescending ? query.OrderByDescending(x => x.LastName) : query.OrderBy(x => x.LastName),
                "companyname" => request.SortDescending ? query.OrderByDescending(x => x.CompanyName) : query.OrderBy(x => x.CompanyName),
                _ => query.OrderByDescending(x => x.CompanyName)
            };
        }

        internal static IQueryable<Contact> ApplyFilter(this IQueryable<Contact> query, ContactFilterRequest filter)
        {

            if (!string.IsNullOrEmpty(filter.ComapnyName))
                query = query.Where(c => c.Company.Name.ToLower().Contains(filter.ComapnyName.ToLower()));

            if (filter.IsPrimary.HasValue)
                query = query.Where(c => c.IsPrimary == filter.IsPrimary.Value);

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
