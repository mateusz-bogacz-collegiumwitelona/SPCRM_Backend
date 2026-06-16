using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Models 
{
    public class Invoice : BaseEntity
    {
        public required string InvoiceNumber { get; set; }
        public long TotalAmount { get; set; } // x10000
        public long PaidAmount { get; set; } // x10000
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }


        public Guid CurrencyId { get; set; }
        public Currency Currency { get; set; } = null!;

        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public Guid? DealId { get; set; }
        public Deal? Deal { get; set; }

        public long RemainingAmount => TotalAmount - PaidAmount;

        public bool IsOverDue => RemainingAmount > 0 && DueDate < DateTime.UtcNow;
    }
}
