using Domain.Models;
using DTO.Request;

namespace Services.Helpers
{
    internal static class ProductQueryExtension
    {
        internal static IQueryable<Product> ApplySearch(this IQueryable<Product> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return query;

            searchTerm = searchTerm.ToLower();

            return query.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.SteelGrade.ToLower().Contains(searchTerm) ||
                p.ProductCategory.Name.ToLower().Contains(searchTerm)
            );
        }

        internal static IQueryable<Product> ApplyFilter(this IQueryable<Product> query, ProductFilterRequest filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.ProductCategory))
                query = query.Where(p => p.ProductCategory.Name.ToLower() == filter.ProductCategory.ToLower());

            if (!string.IsNullOrWhiteSpace(filter.SteelGrade))
                query = query.Where(p => p.SteelGrade.ToLower() == filter.SteelGrade.ToLower());

            return query;
        }

        internal static IQueryable<Product> ApplySorting(this IQueryable<Product> query, SortingRequest request)
        {
            return request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),

                "steelgrade" => request.SortDescending
                    ? query.OrderByDescending(p => p.SteelGrade)
                    : query.OrderBy(p => p.SteelGrade),

                "quantity" => request.SortDescending
                    ? query.OrderByDescending(p => p.StockQuantity)
                    : query.OrderBy(p => p.StockQuantity),

                _ => query.OrderBy(p => p.Name)
            };
        }
    }
}
