using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Helpers
{
    public static class PaginationHelper
    {
        public static async Task<Result<PagedResult<T>>> ToPagedResultAsync<T>(
           this IQueryable<T> source,
           PaggedRequest pagged,
           ILogger logger,
           string entityName = "item"
           )
        {
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source));

                int pageNumber = pagged.PageNumber ?? 1;
                int pageSize = pagged.PageSize ?? 10;

                int totalCount = await source.CountAsync();

                if (totalCount == 0)
                {
                    var emptyPage = CreateEmptyPagedResult<T>(pagged);

                    return Result<PagedResult<T>>.Failure(
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
                    .ToListAsync();

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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while paginating {EntityName}", entityName);

                return Result<PagedResult<T>>.Failure(
                    "An error occurred while processing your request.",
                    StatusCodes.Status500InternalServerError,
                    new List<string> { $"{ex.Message} | {ex.InnerException?.Message}" });
            }
        }

        private static PagedResult<T> CreateEmptyPagedResult<T>(PaggedRequest pagged)
        {
            return new PagedResult<T>
            {
                Items = new List<T>(),
                PageNumber = pagged.PageNumber ?? 1,
                PageSize = pagged.PageSize ?? 10,
                TotalCount = 0,
                TotalPages = 0
            };
        }
    }
}

