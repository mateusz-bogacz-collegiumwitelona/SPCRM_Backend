using Api.Request.Invoice;
using Riok.Mapperly.Abstractions;
using Services.Command.Invoice;

namespace Api.Mappers
{
    [Mapper]
    public partial class InvoiceMapper
    {
        public partial InvoiceListCommand MapList(InvoiceListRequest request);
    }
}
