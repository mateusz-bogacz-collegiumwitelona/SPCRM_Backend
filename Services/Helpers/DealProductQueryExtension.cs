using Domain.Models;
using DTO.Request;

namespace Services.Helpers
{
    internal static class DealProductQueryExtension
    {
        internal static IQueryable<DealProduct> ApplySearch(this IQueryable<DealProduct> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return query;

            searchTerm = searchTerm.ToLower();

            return query.Where(dp =>
                dp.Product.Name.ToLower().Contains(searchTerm) ||
                dp.Product.SteelGrade.ToLower().Contains(searchTerm) ||
                dp.Product.ProductCategory.Name.ToLower().Contains(searchTerm)
            );
        }

        internal static IQueryable<DealProduct> ApplyFilter(this IQueryable<DealProduct> query, ProductFilterRequest filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.ProductCategory))
                query = query.Where(dp => dp.Product.ProductCategory.Name.ToLower() == filter.ProductCategory.ToLower());

            if (!string.IsNullOrWhiteSpace(filter.SteelGrade))
                query = query.Where(dp => dp.Product.SteelGrade.ToLower() == filter.SteelGrade.ToLower());

            return query;
        }

        internal static IQueryable<DealProduct> ApplySorting(this IQueryable<DealProduct> query, SortingRequest request)
        {
            return request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending
                    ? query.OrderByDescending(dp => dp.Product.Name)
                    : query.OrderBy(dp => dp.Product.Name),

                "steelgrade" => request.SortDescending
                    ? query.OrderByDescending(dp => dp.Product.SteelGrade)
                    : query.OrderBy(dp => dp.Product.SteelGrade),

                "quantity" => request.SortDescending
                    ? query.OrderByDescending(dp => dp.Quantity)
                    : query.OrderBy(dp => dp.Quantity),

                "totalprice" => request.SortDescending
                    ? query.OrderByDescending(dp => dp.Quantity * dp.UnitPrice)
                    : query.OrderBy(dp => dp.Quantity * dp.UnitPrice),

                _ => query.OrderBy(dp => dp.Product.Name)
            };
        }
    }
}
