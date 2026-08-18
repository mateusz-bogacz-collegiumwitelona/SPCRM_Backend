using Domain.Common;

namespace Domain.Models
{
    public class Promotion : BaseEntity
    {
        public required string Name { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal? DiscountPercentage { get; set; }

        public long? PromotionalPrice { get; set; }
        public Guid? CurrencyId { get; set; }
        public Currency? Currency { get; set; }

        public bool IsActive { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public Guid? ContactId { get; set; }
        public int? MinQuantity { get; set; }
        public int? MinWeight { get; set; }
    }
}
