using Domain;
using DTO.Request;
using DTO.Response;


namespace Services.Helpers
{
    internal static class ContactQueryExtension
    {
        internal static IQueryable<ContactsResponse> ApplySorting(this IQueryable<ContactsResponse> query, SearchRequest request)
        {
            return request.SortBy?.ToLower() switch 
            {
                "firstname" => request.SortDescending ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName),
                "lastname" => request.SortDescending ? query.OrderByDescending(x => x.LastName) : query.OrderBy(x => x.LastName),
                "company" => request.SortDescending ? query.OrderByDescending(x => x.CompanyName) : query.OrderBy(x => x.CompanyName),
                "ownerfirstname" => request.SortDescending ? query.OrderByDescending(x => x.OwnerFirstName) : query.OrderBy(x => x.OwnerFirstName),
                "ownerlastname" => request.SortDescending ? query.OrderByDescending(x => x.OwnerLastName) : query.OrderBy(x => x.OwnerLastName),
                _ => query.OrderByDescending(x => x.CompanyName)
            };
        }

        internal static IQueryable<Contact> ApplyFilter(this IQueryable<Contact> query, ContactFilterRequest filter , string searchTerm)
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

            if (!string.IsNullOrEmpty(filter.ComapnyName))
                query = query.Where(c => c.Company.Name.ToLower().Contains(filter.ComapnyName.ToLower()));

            if (filter.IsPrimary.HasValue)
                query = query.Where(c => c.ContactDetails.Any(cd => cd.IsPrimary) == filter.IsPrimary.Value);

            return query;
        }

     }
}
