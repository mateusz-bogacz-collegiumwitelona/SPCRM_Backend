using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class Product : BaseEntity
    {
        public required string Name { get; set; }
        public required string SteelGrade { get; set; }
        public int Thickness { get; set; }
        public int Width { get; set; }
        public int Length { get; set; }
        public int? Diameter { get; set; }
        public int Weight { get; set; } // kg * 1000
        public Guid UnitId { get; set; }
        public UnitOfMeasure Unit { get; set; } = null!;

        public long PricePerUnit { get; set; }
        public int StockQuantity { get; set; }

        public ProductCategoryEnum Category { get; set; }
        public ICollection<DealProduct> DealProducts { get; set; } = new List<DealProduct>();
    }
}
