using Domain.Common;
using DTO.Request;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Services.Helpers
{
    public static class PaginationHelper
    {
        public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var totalCount = source.Count();
            var items = source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0
            };
        }

        public static PagedResult<T> ToPagedResult<T>(this List<T> source, int pageNumber, int pageSize)
            => ((IEnumerable<T>)source).ToPagedResult(pageNumber, pageSize);

        public static async Task<Result<PagedResult<T>>> ToPagedResultAsync<T>(
           this Task<List<T>> dataTask,
           PaggedRequest pagged,
           ILogger logger,
           string entityName = "item"
           )
        {
            try
            {
                var result = await dataTask;

                if (result == null || !result.Any())
                {
                    var emptyPage = CreateEmptyPagedResult<T>(pagged);

                    return Result<PagedResult<T>>.Failure(
                        message: $"No {entityName} found.",
                        statusCode: StatusCodes.Status200OK,
                        data: emptyPage
                        );
                }

                int pageNumber = pagged.PageNumber ?? 1;
                int pageSize = pagged.PageSize ?? 10;

                var pagedResult = result.ToPagedResult(pageNumber, pageSize);

                if (pagedResult.PageNumber > pagedResult.TotalPages && pagedResult.TotalPages > 0)
                    pagedResult = result.ToPagedResult(pagedResult.TotalPages, pageSize);

                return Result<PagedResult<T>>.Success(
                    message: $"{entityName} retrieved successfully",
                    statusCode: StatusCodes.Status200OK,
                    data: pagedResult
                    );
            }
            catch (Exception ex)
            {
                return Result<PagedResult<T>>.Failure(
                    "An error occurred while processing your request.",
                    StatusCodes.Status500InternalServerError,
                    new List<string> { $"{ex.Message} | {ex.InnerException}" });
            }
        }

        private static PagedResult<T> CreateEmptyPagedResult<T>(PaggedRequest pagged)
            => new PagedResult<T>
            {
                Items = new List<T>(),
                PageNumber = pagged.PageNumber ?? 1,
                PageSize = pagged.PageSize ?? 10,
                TotalCount = 0,
                TotalPages = 0
            };
    }
}
