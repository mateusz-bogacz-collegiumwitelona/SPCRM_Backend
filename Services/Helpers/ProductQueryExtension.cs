using Domain.Models;

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

        internal static IQueryable<Product> ApplyFilter(this IQueryable<Product> query, string? productCategory, string? steelGrade)
        {
            if (!string.IsNullOrWhiteSpace(productCategory))
                query = query.Where(p => p.ProductCategory.Name.ToLower() == productCategory.ToLower());

            if (!string.IsNullOrWhiteSpace(steelGrade))
                query = query.Where(p => p.SteelGrade.ToLower() == steelGrade.ToLower());

            return query;
        }

        internal static IQueryable<Product> ApplySorting(this IQueryable<Product> query, string? sortBy, bool sortDescending)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),

                "steelgrade" => sortDescending
                    ? query.OrderByDescending(p => p.SteelGrade)
                    : query.OrderBy(p => p.SteelGrade),

                "quantity" => sortDescending
                    ? query.OrderByDescending(p => p.StockQuantity)
                    : query.OrderBy(p => p.StockQuantity),

                _ => query.OrderBy(p => p.Name)
            };
        }
    }
}
