using Domain.Common;

namespace Domain.Models
{
    public class InvoiceProducts : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public required string ProductName { get; set; }
        public string? SteelGrade { get; set; }
        public required string UnitSymbol { get; set; }
        public int Quantity { get; set; }
        public long UnitPrice { get; set; } // x10000
        public long TotalPrice => (long)Quantity * UnitPrice;
    }
}
