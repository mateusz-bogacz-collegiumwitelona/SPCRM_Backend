using Domain.Comunication;
using Domain.Enum;
using Domain.Events;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Handlers
{
    public class DealCompletedEventHandler : INotificationHandler<DealCompletedEvent>
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<DealCompletedEventHandler> _logger;

        public DealCompletedEventHandler(
            AppDbContext context,
            IEmailSender emailSender,
            ILogger<DealCompletedEventHandler> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task Handle(DealCompletedEvent notification, CancellationToken cancellationToken)
        {
            var deal = await _context.Deals
                .Include(d => d.Currency)
                .Include(d => d.Company)
                    .ThenInclude(c => c.Contacts)
                        .ThenInclude(ct => ct.ContactDetails)
                .Include(d => d.DealProducts)
                    .ThenInclude(dp => dp.Product)
                        .ThenInclude(p => p.SteelGrade)
                .Include(d => d.DealProducts)
                    .ThenInclude(dp => dp.Product)
                        .ThenInclude(p => p.Unit)
                .FirstOrDefaultAsync(d => d.Id == notification.DealId, cancellationToken);

            if (deal == null)
            {
                _logger.LogError("Cannot create invoice: Deal {DealId} not found.", notification.DealId);
                return;
            }

            var invoice = await BuildInvoiceForDealAsync(deal, cancellationToken);
            await _context.Invoices.AddAsync(invoice, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Invoice {InvoiceNumber} created for Deal {DealId}.", invoice.InvoiceNumber, deal.Id);

            await DispatchInvoiceEmailAsync(invoice, deal, notification.RecipientEmail, notification.Language);
        }

        private async Task<Invoice> BuildInvoiceForDealAsync(Deal deal, CancellationToken cancellationToken)
        {
            var year = DateTime.UtcNow.Year;
            var month = DateTime.UtcNow.Month;
            var count = await _context.Invoices.CountAsync(
                i => i.IssueDate.Year == year && i.IssueDate.Month == month,
                cancellationToken);

            var totalAmount = deal.DealProducts.Sum(dp => (long)dp.Quantity * dp.UnitPrice);

            return new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"FV/{year:0000}/{month:00}/{count + 1:0000}",
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(14),
                TotalAmount = totalAmount,
                PaidAmount = 0,
                DealId = deal.Id,
                CompanyId = deal.CompanyId,
                CurrencyId = deal.CurrencyId,
                InvoiceProducts = deal.DealProducts.Select(dp => new InvoiceProducts
                {
                    Id = Guid.NewGuid(),
                    ProductId = dp.ProductId,
                    ProductName = dp.Product.Name,
                    SteelGrade = dp.Product.SteelGrade?.Name,
                    UnitSymbol = dp.Product.Unit.Symbol,
                    Quantity = dp.Quantity,
                    UnitPrice = dp.UnitPrice
                }).ToList()
            };
        }


        private async Task DispatchInvoiceEmailAsync(Invoice invoice, Deal deal, string? recipientEmail, string? language)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning(
                    "Deal {DealId} completed and Invoice {InvoiceNumber} was created, but no contact email was found.",
                    deal.Id, invoice.InvoiceNumber);
                return;
            }

            var invoiceMailPayload = new InvoiceEmailDomain
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                RecipientEmail = recipientEmail,
                RecipientName = deal.Company.Name,
                TotalGrossAmount = invoice.TotalAmount / 10000m,
                CurrencyCode = deal.Currency.Code,
                DueDate = invoice.DueDate,
                Language = language ?? "pl"
            };

            await _emailSender.SendInvoiceEmailAsync(invoiceMailPayload);
        }
    }
}
