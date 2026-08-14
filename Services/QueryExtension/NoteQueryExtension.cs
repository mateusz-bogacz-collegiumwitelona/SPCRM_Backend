using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class NoteQueryExtension
    {
        internal static IQueryable<Note> ApplySearch(this IQueryable<Note> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    string wildcardTerm = $"%{term}%";

                    query = query.Where(n =>
                        EF.Functions.ILike(EF.Functions.Unaccent(n.Title), EF.Functions.Unaccent(wildcardTerm)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(n.Content), EF.Functions.Unaccent(wildcardTerm)) ||
                        (n.Author != null && (
                            EF.Functions.ILike(EF.Functions.Unaccent(n.Author.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                            EF.Functions.ILike(EF.Functions.Unaccent(n.Author.LastName), EF.Functions.Unaccent(wildcardTerm))
                        ))
                    );
                }
            }

            return query;
        }
    }
}
