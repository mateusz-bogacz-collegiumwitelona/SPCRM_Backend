using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class UserQueryExtension
    {
        internal static IQueryable<ApplicationUser> ApplySearch(
            this IQueryable<ApplicationUser> query,
            string? searchTerm,
            AppDbContext context)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return query;

            var terms = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                string wildcardTerm = $"%{term}%";

                query = query.Where(u =>
                    EF.Functions.ILike(EF.Functions.Unaccent(u.FirstName), EF.Functions.Unaccent(wildcardTerm)) ||
                    EF.Functions.ILike(EF.Functions.Unaccent(u.LastName), EF.Functions.Unaccent(wildcardTerm)) ||
                    context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                        .Any(roleName => roleName != null && EF.Functions.ILike(EF.Functions.Unaccent(roleName), EF.Functions.Unaccent(wildcardTerm)))
                );
            }

            return query;
        }

        internal static IQueryable<ApplicationUser> ApplyFilter(
            this IQueryable<ApplicationUser> query,
            string? role,
            bool? isBlocked,
            AppDbContext context)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .Any(r => r == role));
            }

            if (isBlocked.HasValue)
            {
                var now = DateTimeOffset.UtcNow;
                query = isBlocked.Value
                    ? query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now)
                    : query.Where(u => u.LockoutEnd == null || u.LockoutEnd <= now);
            }

            return query;
        }

        internal static IQueryable<ApplicationUser> ApplySorting(
            this IQueryable<ApplicationUser> query,
            string? sortBy,
            bool sortDescending,
            AppDbContext context)
            => sortBy?.ToLower() switch
            {
                "firstname" => sortDescending
                    ? query.OrderByDescending(u => u.FirstName)
                    : query.OrderBy(u => u.FirstName),

                "lastname" => sortDescending
                    ? query.OrderByDescending(u => u.LastName)
                    : query.OrderBy(u => u.LastName),

                "role" => sortDescending
                    ? query.OrderByDescending(u => context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                        .FirstOrDefault())
                    : query.OrderBy(u => context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                        .FirstOrDefault()),

                "isblocked" => sortDescending
                    ? query.OrderByDescending(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow)
                    : query.OrderBy(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow),

                _ => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            };
    }
}
