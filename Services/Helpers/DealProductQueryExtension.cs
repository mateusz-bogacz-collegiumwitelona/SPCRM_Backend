using Domain.Models;

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

        internal static IQueryable<DealProduct> ApplyFilter(this IQueryable<DealProduct> query, string? productCategory, string? steelGrade)
        {
            if (!string.IsNullOrWhiteSpace(productCategory))
                query = query.Where(dp => dp.Product.ProductCategory.Name.ToLower() == productCategory.ToLower());

            if (!string.IsNullOrWhiteSpace(steelGrade))
                query = query.Where(dp => dp.Product.SteelGrade.ToLower() == steelGrade.ToLower());

            return query;
        }

        internal static IQueryable<DealProduct> ApplySorting(this IQueryable<DealProduct> query, string? sortBy, bool sortDescending)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortDescending
                    ? query.OrderByDescending(dp => dp.Product.Name)
                    : query.OrderBy(dp => dp.Product.Name),

                "steelgrade" => sortDescending
                    ? query.OrderByDescending(dp => dp.Product.SteelGrade)
                    : query.OrderBy(dp => dp.Product.SteelGrade),

                "quantity" => sortDescending
                    ? query.OrderByDescending(dp => dp.Quantity)
                    : query.OrderBy(dp => dp.Quantity),

                "totalprice" => sortDescending
                    ? query.OrderByDescending(dp => dp.Quantity * dp.UnitPrice)
                    : query.OrderBy(dp => dp.Quantity * dp.UnitPrice),

                _ => query.OrderBy(dp => dp.Product.Name)
            };
        }
    }
}
