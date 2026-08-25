using Domain.Common;

namespace Domain.Models
{
    public class SteelGrade : BaseEntity
    {
        public required string Name { get; set; }
        public string? Standard { get; set; }
        public int Density { get; set; } = 7850; // kg/m3
        
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
