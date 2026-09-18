using Domain.Common;

namespace Domain.Models
{
    public class InvoicePayment : BaseEntity
    {
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public required long Amount { get; set; } // x10000
        public required DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Note { get; set; }

        public Guid? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }
    }
}
