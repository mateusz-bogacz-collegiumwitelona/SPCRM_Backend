using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
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

        internal static IQueryable<Contact> ApplyFilter(this IQueryable<Contact> query, string? companyName, bool? isPrimary, Guid? ownerId)
        {

            if (!string.IsNullOrEmpty(companyName))
                query = query.Where(c => c.Company.Name.ToLower().Contains(companyName.ToLower()));

            if (isPrimary.HasValue)
                query = query.Where(c => c.IsPrimary == isPrimary.Value);

            if (ownerId.HasValue)
                query = query.Where(c => c.OwnerId == ownerId);

            return query;
        }

        internal static IQueryable<Contact> ApplySearch(this IQueryable<Contact> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.ILike(EF.Functions.Unaccent(c.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.LastName), EF.Functions.Unaccent(wildcardTerm)) ||
                        (c.Company != null && EF.Functions.ILike(EF.Functions.Unaccent(c.Company.Name), EF.Functions.Unaccent(wildcardTerm))) ||
                        (c.Owner != null && (
                            EF.Functions.ILike(EF.Functions.Unaccent(c.Owner.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                            EF.Functions.ILike(EF.Functions.Unaccent(c.Owner.LastName), EF.Functions.Unaccent(wildcardTerm))
                        ))
                    );
                }
            }
            return query;
        }
    }
}
