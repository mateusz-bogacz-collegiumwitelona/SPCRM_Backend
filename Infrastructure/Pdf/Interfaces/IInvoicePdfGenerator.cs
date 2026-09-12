using Domain.Models;

namespace Infrastructure.Pdf.Interfaces
{
    public interface IInvoicePdfGenerator
    {
        byte[] GenerateInvoicePdf(Invoice invoice, string language);
    }
}
