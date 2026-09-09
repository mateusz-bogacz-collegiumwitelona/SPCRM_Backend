using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class TaskQueryExtension
    {
        internal static IQueryable<Tasks> ApplyFilter(this IQueryable<Tasks> query,
            TaskStatusEnum? status,
            TaskPriorityEnum? priority)
        {
            if (status.HasValue) query = query.Where(t => t.Status == status.Value);

            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            return query;
        }

        internal static IQueryable<Tasks> ApplySearch(this IQueryable<Tasks> query, string searchTerm)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var wildcard = $"%{searchTerm.Trim()}%";
                query = query.Where(t =>
                    EF.Functions.ILike(EF.Functions.Unaccent(t.Title), EF.Functions.Unaccent(wildcard)) ||
                    (t.Deal != null && EF.Functions.ILike(EF.Functions.Unaccent(t.Deal.Name), EF.Functions.Unaccent(wildcard))) ||
                    (t.Contact != null && (
                        EF.Functions.ILike(EF.Functions.Unaccent(t.Contact.FirstName), EF.Functions.Unaccent(wildcard)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(t.Contact.LastName), EF.Functions.Unaccent(wildcard))
                    ))
                );
            }
            return query;
        }

        internal static IQueryable<Tasks> ApplySorting(
            this IQueryable<Tasks> query,
            string? sortBy,
            bool sortDescending
            )
            => sortBy?.ToLower() switch
            {
                "title" => sortDescending ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "priority" => sortDescending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                _ => sortDescending ? query.OrderByDescending(t => t.DueAt) : query.OrderBy(t => t.DueAt)
            };
    }
}
