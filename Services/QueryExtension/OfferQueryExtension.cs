using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class OfferQueryExtension
    {
        internal static IQueryable<Offer> ApplySearch(this IQueryable<Offer> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(c =>
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Contact.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Contact.LastName), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(c.Contact.Company.Name), EF.Functions.Unaccent(wildcardTerm))
                    );
                }
            }

            return query;
        }

        internal static IQueryable<Offer> ApplyFilter(
            this IQueryable<Offer> query,
            DateTime? validUntilFrom,
            DateTime? validUntilTo,
            string? companyName,
            OfferStatusEnum? status
            )
        {
            if (validUntilFrom.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(validUntilFrom.Value, DateTimeKind.Utc);
                query = query.Where(o => o.ValidUntil >= fromUtc);
            }

            if (validUntilTo.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(validUntilTo.Value, DateTimeKind.Utc);
                query = query.Where(o => o.ValidUntil <= toUtc);
            }

            if (!string.IsNullOrEmpty(companyName))
            {
                string wildcardCompanyName = $"%{companyName}%";
                query = query.Where(o => EF.Functions.ILike(EF.Functions.Unaccent(o.Contact.Company.Name), EF.Functions.Unaccent(wildcardCompanyName)));
            }

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            return query;
        }

        internal static IQueryable<Offer> ApplySorting(
            this IQueryable<Offer> query,
            string? sortBy,
            bool sortDescending)
            => sortBy?.ToLower() switch
            {
                "validuntil" => sortDescending
                    ? query.OrderByDescending(o => o.ValidUntil)
                    : query.OrderBy(o => o.ValidUntil),

                "companyname" => sortDescending
                    ? query.OrderByDescending(o => o.Contact.Company.Name)
                    : query.OrderBy(o => o.Contact.Company.Name),

                "status" => sortDescending
                    ? query.OrderByDescending(o => o.Status)
                    : query.OrderBy(o => o.Status),

                _ => query.OrderBy(o => o.ValidUntil)
            };
    }
}
