using Domain.Enum;
using Domain.Models;

namespace Services.Helpers
{
    internal static class TaskExtensionQuery
    {
        internal static IQueryable<Tasks> ApplyFilterByStatus(this IQueryable<Tasks> query, string? status)
        {
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TaskStatusEnum>(status, true, out var parsedStatus))
            {
                query = query.Where(t => t.Status == parsedStatus);
            }
            return query;
        }

        internal static IQueryable<Tasks> ApplyFilterByPriority(this IQueryable<Tasks> query, string? priority)
        {
            if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TaskPriorityEnum>(priority, true, out var parsedPriority))
            {
                query = query.Where(t => t.Priority == parsedPriority);
            }
            return query;
        }
    }
}
