namespace Services.Helpers
{
    internal static class DimensionsFormatter
    {
        internal static string Format(string category, string type, int? diameter, int thickness, int width, int length)
        {
            if (category.Contains("Rury", StringComparison.OrdinalIgnoreCase) || diameter.HasValue)
            {
                string d = diameter.HasValue 
                    ? diameter.Value.ToString() 
                    : (width > 0 ? width.ToString() : "?");

                return $"fi {d} x {thickness} (L={length})";
            }

            if (type.Contains("Pręt", StringComparison.OrdinalIgnoreCase))
                return diameter.HasValue
                    ? $"fi {diameter} (L={length})"
                    : $"{width} x {thickness} (L={length})";

            if (type.Contains("Profil", StringComparison.OrdinalIgnoreCase) || type.Contains("Kątownik", StringComparison.OrdinalIgnoreCase))
                return $"{width} x {thickness} (L={length})";

            return $"{thickness} x {width} x {length}";
        }
    }
}
