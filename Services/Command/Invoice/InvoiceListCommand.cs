namespace Services.Command.Invoice
{
    public record InvoiceListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } 
        public string? SearchTerm { get; init; }
        public string? CompanyName { get; init; }
        public string? CompanyNip { get; init; }
        public DateTime? IssueDateFrom { get; init; }
        public DateTime? IssueDateTo { get; init; }
        public long? TotalAmountFrom { get; init; }
        public long? TotalAmountTo { get; init; }
        public bool? IsOverDue { get; init; }
    }
}
