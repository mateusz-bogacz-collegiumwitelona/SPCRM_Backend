using Domain.Common;
using Services.Command.Company;
using Services.Command.Invoice;
using Services.Command.List;
using Services.Response.Company;
using Services.Response.Invoice;

namespace Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid comapnyId);
        Task<Result<PagedResult<CompanyDebtDetailResponse>>> GetCompanyDebtsAsync(CompanyCommand command);
        Task<Result<PagedResult<InvoiceResponse>>> GetInvoiceListAsync(InvoiceListCommand command);
        Task<Result<InvoiceDetailResponse>> GetInvoiceDetailAsync(Guid invoiceId);
        Task<Result<PagedResult<InvoiceProductsListResponse>>> GetInvoiceProductAsync(Guid invoiceId, SimpleListCommand command);
        Task<Result<InvoicePaymentSummaryResponse>> GetInvoicePaymentSummaryAsync(Guid invoiceId);
    }
}
