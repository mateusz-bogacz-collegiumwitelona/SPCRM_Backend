using Domain.Common;

namespace Services.Helpers
{
    internal static class NoteQueryExtension
    {
        internal static IQueryable<Note> ApplySearch(this IQueryable<Note> query, string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(n => 
                    n.Title.ToLower().Contains(searchTerm) ||
                    n.Content.ToLower().Contains(searchTerm) ||
                    n.Author.FirstName.ToLower().Contains(searchTerm) ||
                    n.Author.LastName.ToLower().Contains(searchTerm));
            }

            return query;
        }
    }
}
