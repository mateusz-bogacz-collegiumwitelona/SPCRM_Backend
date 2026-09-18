using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class InvoiceQueryExtension
    {
        internal static IQueryable<Invoice> ApplyFilter(
            this IQueryable<Invoice> query,
            string? companyName,
            string? companyNip,
            DateTime? issueDateFrom,
            DateTime? issueDateTo,
            long? totalAmountFrom,
            long? totalAmountTo,
            bool? isOverDue
            )
        {
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var search = companyName.ToLower();
                query = query.Where(i => i.Company.Name.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(companyNip))
            {
                var search = companyNip.ToLower();
                query = query.Where(i => i.Company.NIP.ToLower().Contains(search));
            }

            if (issueDateFrom.HasValue)
                query = query.Where(i => i.IssueDate >= issueDateFrom.Value.ToUniversalTime());

            if (issueDateTo.HasValue)
                query = query.Where(i => i.IssueDate <= issueDateTo.Value.ToUniversalTime());
            
            if (totalAmountFrom.HasValue)
                query = query.Where(i => i.TotalAmount >= totalAmountFrom.Value);
            
            if (totalAmountTo.HasValue)
                query = query.Where(i => i.TotalAmount <= totalAmountTo.Value);

            if (isOverDue.HasValue)
            {
                var now = DateTime.UtcNow;
                if (isOverDue.Value)
                {
                    query = query.Where(i => (i.TotalAmount - i.PaidAmount) > 0 && i.DueDate < now);
                }
                else
                {
                    query = query.Where(i => (i.TotalAmount - i.PaidAmount) <= 0 || i.DueDate >= now);
                }
            }

            return query;
        }

        internal static IQueryable<Invoice> ApplySorting(this IQueryable<Invoice> query, string? sortBy, bool sortDescending)
            => sortBy?.ToLower() switch
            {
                "totalamount" => sortDescending
                    ? query.OrderByDescending(x => x.TotalAmount)
                    : query.OrderBy(x => x.TotalAmount),
                
                "issueDate" => sortDescending
                    ? query.OrderByDescending(x => x.IssueDate)
                    : query.OrderBy(x => x.IssueDate),
              
                "dueDate" => sortDescending
                    ? query.OrderByDescending(x => x.DueDate)
                    : query.OrderBy(x => x.DueDate),
                
                _ => query.OrderByDescending(x => x.IssueDate)
            };

        internal static IQueryable<Invoice> ApplySearch(this IQueryable<Invoice> query, string? searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(i =>
                        EF.Functions.ILike(EF.Functions.Unaccent(i.InvoiceNumber), EF.Functions.Unaccent(wildcardTerm)) ||
                        (i.Company != null && EF.Functions.ILike(EF.Functions.Unaccent(i.Company.Name), EF.Functions.Unaccent(wildcardTerm))) ||
                        (i.Company != null && EF.Functions.ILike(EF.Functions.Unaccent(i.Company.NIP), EF.Functions.Unaccent(wildcardTerm))) ||
                        (i.Currency != null && EF.Functions.ILike(EF.Functions.Unaccent(i.Currency.Code), EF.Functions.Unaccent(wildcardTerm)))
                    );
                }
            }
            return query;
        }
    }
}
