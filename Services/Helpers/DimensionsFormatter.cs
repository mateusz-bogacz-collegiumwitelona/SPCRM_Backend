using Domain.Enum;

namespace Services.Helpers
{
    public static class DimensionsFormatter
    {
        public static string Format(ProductCategoryEnum categoryCode, int? diameter, int thickness, int width, int length)
        {
            return categoryCode switch
            {
                ProductCategoryEnum.Pipe =>
                    $"fi {(diameter.HasValue ? diameter.Value.ToString() : (width > 0 ? width.ToString() : "?"))} x {thickness} (L={length})",

                ProductCategoryEnum.Bar =>
                    diameter.HasValue ? $"fi {diameter} (L={length})" : $"{width} x {thickness} (L={length})",

                ProductCategoryEnum.Profile =>
                    $"{width} x {thickness} (L={length})",

                ProductCategoryEnum.Standard or _ =>
                    $"{thickness} x {width} x {length}"
            };
        }
    }
}
