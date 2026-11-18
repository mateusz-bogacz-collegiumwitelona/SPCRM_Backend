using Services.Command.List;

namespace Services.Command.Product
{
    public record ProductListCommand : BasicListCommand
    {
        public string? ProductCategory { get; init; }
        public string? SteelGrade { get; init; }
        public bool? HasActivePromotion { get; init; }
    }
}
