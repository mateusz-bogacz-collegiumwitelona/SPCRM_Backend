using Domain.Enum;
using System.Globalization;

namespace Services.Helpers
{
    public static class DimensionsFormatter
    {
        public static string Format(
            ProductCategoryEnum categoryCode,
            int? diameter,
            int thickness,
            int width,
            int length
        )
        {
            var inv = CultureInfo.InvariantCulture;

            double t = thickness / 10.0;
            double w = width / 10.0;
            double l = length / 10.0;
            double? d = diameter.HasValue ? diameter.Value / 10.0 : null;

            string FormatNumber(double val) => val.ToString("0.##", inv);

            return categoryCode switch
            {
                ProductCategoryEnum.Pipe =>
                    $"fi {(d.HasValue ? FormatNumber(d.Value) : (w > 0 ? FormatNumber(w) : "?"))} x {FormatNumber(t)} (L={FormatNumber(l)})",

                ProductCategoryEnum.Bar =>
                    d.HasValue ? $"fi {FormatNumber(d.Value)} (L={FormatNumber(l)})" : $"{FormatNumber(w)} x {FormatNumber(t)} (L={FormatNumber(l)})",

                ProductCategoryEnum.Profile or ProductCategoryEnum.Beam =>
                    $"{FormatNumber(w)} x {FormatNumber(t)} (L={FormatNumber(l)})",

                ProductCategoryEnum.Wire =>
                    d.HasValue ? $"fi {FormatNumber(d.Value)}" : $"fi {FormatNumber(t)}",

                ProductCategoryEnum.Sheet or ProductCategoryEnum.Mesh =>
                    $"{FormatNumber(t)} x {FormatNumber(w)} x {FormatNumber(l)}",

                ProductCategoryEnum.Fitting or ProductCategoryEnum.Other or _ =>
                    d.HasValue ? $"fi {FormatNumber(d.Value)} x {FormatNumber(l)}" : $"{FormatNumber(t)} x {FormatNumber(w)} x {FormatNumber(l)}"
            };
        }
    }
}
