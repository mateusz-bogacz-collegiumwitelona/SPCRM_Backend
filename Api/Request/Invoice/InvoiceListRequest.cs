using Api.Request.List;

namespace Api.Request.Invoice
{
    public record InvoiceListRequest : BasicListRequest
    {
        public string? CompanyName { get; init; }
        public string? CompanyNip { get; init; }
        public DateTime? IssueDateFrom { get; init; }
        public DateTime? IssueDateTo { get; init; }
        public long? TotalAmountFrom { get; init; }
        public long? TotalAmountTo { get; init; }
        public bool? IsOverDue { get; init; }
    }
}
