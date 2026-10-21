using Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Helpers
{
    public static class PaginationHelper
    {
        public static async Task<Result<PagedResult<T>>> ToPagedResultAsync<T>(
           this IQueryable<T> source,
           int? number,
           int? size,
           ILogger logger,
           string entityName = "item",
           CancellationToken cancellationToken = default
           )
        {

            if (source == null) throw new ArgumentNullException(nameof(source));

            int pageNumber = Math.Max(number ?? 1, 1);
            int pageSize = Math.Clamp(size ?? 10, 1, 100);

            int totalCount = await source.CountAsync(cancellationToken);

            if (totalCount == 0)
            {
                var emptyPage = CreateEmptyPagedResult<T>(number, size);

                return Result<PagedResult<T>>.Success(
                    message: $"No {entityName} found.",
                    statusCode: StatusCodes.Status200OK,
                    data: emptyPage
                    );
            }

            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (pageNumber > totalPages && totalPages > 0)
            {
                pageNumber = totalPages;
            }

            var items = await source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var pagedResult = new PagedResult<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Result<PagedResult<T>>.Success(
                message: $"{entityName} retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: pagedResult
                );
        }

        public static PagedResult<T> CreateEmptyPagedResult<T>(int? number, int? size)
            => new PagedResult<T>
            {
                Items = new List<T>(),
                PageNumber = number ?? 1,
                PageSize = size ?? 10,
                TotalCount = 0,
                TotalPages = 0
            };
    }
}
