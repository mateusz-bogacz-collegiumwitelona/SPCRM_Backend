using Domain.Common;
using Domain.Enum;

namespace Domain.Models
{
    public class Product : BaseEntity
    {
        public required string Name { get; set; }
        
        public required Guid SteelGradeId { get; set; }
        public required SteelGrade SteelGrade { get; set; }

        public int Thickness { get; set; } // mm * 10
        public int Width { get; set; } // mm * 10
        public int Length { get; set; } // mm * 10
        public int? Diameter { get; set; } // mm * 10
        public int Weight { get; set; } // kg * 1000
        public Guid UnitId { get; set; }
        public UnitOfMeasure Unit { get; set; } = null!;

        public long PricePerUnit { get; set; }
        public Guid CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        public int StockQuantity { get; set; }

        public ProductCategoryEnum Category { get; set; }
        public ICollection<DealProduct> DealProducts { get; set; } = new List<DealProduct>();

        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    }
}
