using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class CompanyQueryExtension
    {
        internal static IQueryable<Company> ApplySearch(this IQueryable<Company> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.NIP), EF.Functions.Unaccent(wildcardTerm)) ||
                        c.CompanyAdresses.Any(a =>
                            EF.Functions.ILike(EF.Functions.Unaccent(a.City), EF.Functions.Unaccent(wildcardTerm)) ||
                            EF.Functions.ILike(EF.Functions.Unaccent(a.Street), EF.Functions.Unaccent(wildcardTerm)) ||
                            EF.Functions.ILike(EF.Functions.Unaccent(a.ZipCode), EF.Functions.Unaccent(wildcardTerm))
                        ) ||
                        (c.Owner != null && (
                            EF.Functions.ILike(EF.Functions.Unaccent(c.Owner.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                            EF.Functions.ILike(EF.Functions.Unaccent(c.Owner.LastName), EF.Functions.Unaccent(wildcardTerm))
                        ))
                    );
                }
            }

            return query;
        }

        internal static IQueryable<Company> ApplyFiler(
            this IQueryable<Company> query,
            bool? isYour,
            DateTime? createdAtFrom,
            DateTime? createdAtTo,
            Guid userId)
        {

            if (isYour.HasValue)
            {
                if (isYour.Value)
                {
                    query = query.Where(c => c.OwnerId == userId);
                }
                else
                {
                    query = query.Where(c => c.OwnerId != userId);
                }

            }

            if (createdAtFrom.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= createdAtFrom.Value.ToUniversalTime());
            }

            if (createdAtTo.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= createdAtTo.Value.ToUniversalTime());
            }

            return query;
        }

        internal static IQueryable<Company> ApplySorting(
            this IQueryable<Company> query,
            string? sortBy,
            bool sortDescending
            )
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(x => x.Name)
                    : query.OrderBy(x => x.Name),

                "nip" => sortDescending
                    ? query.OrderByDescending(x => x.NIP)
                    : query.OrderBy(x => x.NIP),

                "city" => sortDescending
                    ? query.OrderByDescending(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.City)
                        .FirstOrDefault())
                    : query.OrderBy(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.City)
                        .FirstOrDefault()),

                "zipcode" => sortDescending
                    ? query.OrderByDescending(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.ZipCode)
                        .FirstOrDefault())
                    : query.OrderBy(x => x.CompanyAdresses
                        .Where(ca => ca.AddressType == AddressTypeEnum.Headquarters)
                        .Select(ca => ca.ZipCode)
                        .FirstOrDefault()),

                "createdat" => sortDescending
                  ? query.OrderByDescending(x => x.CreatedAt)
                  : query.OrderBy(x => x.CreatedAt),

                _ => query.OrderBy(x => x.Name)
            };
        }
    }
}
