using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.Response;


namespace Services.Services
{
    public class DebtService : IDebtService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DebtService> _logger;

        public DebtService (AppDbContext context, ILogger<DebtService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<CompanyDebtSummaryResponse>>> GetCompanyDebtSummaryAsync(Guid comapnyId)
        {
            var summary = await _context.Invoices
                .Where(i => i.CompanyId == comapnyId && i.PaidAmount < i.TotalAmount)
                .GroupBy(i => new
                {
                    i.Currency.Code,
                    i.Currency.DecimalPlaces
                })
                .Select(g => new CompanyDebtSummaryResponse
                {
                    CurrencyCode = g.Key.Code,
                    DecimalPlace = g.Key.DecimalPlaces,
                    TotalAmount = (decimal)g.Sum(i => i.TotalAmount - i.PaidAmount) / 10000m
                })
                .ToListAsync();

            return Result<List<CompanyDebtSummaryResponse>>.Success(
                message: "Deby summary retrived successfully",
                statusCode: StatusCodes.Status200OK,
                data: summary
                );
        }

        public async Task<Result<PagedResult<CompanyDebtDetailResponse>>> GetCompanyDebtsAsync(CompanyCommand command)
        {
            DateTime now = DateTime.UtcNow;

            var query = _context.Invoices
                .Where(i => i.CompanyId == command.CompanyId && i.PaidAmount < i.TotalAmount)
                .OrderBy(i => i.DueDate)
                .Select(i => new CompanyDebtDetailResponse
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    AmountLeft = (decimal)(i.TotalAmount - i.PaidAmount) / 10000m,
                    CurrencyCode = i.Currency.Code,
                    DecimalPlaces = i.Currency.DecimalPlaces,
                    DueDate = i.DueDate,
                    DaysOverdue = i.DueDate < now ? (int)(now - i.DueDate).TotalDays : 0
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_debt");
        }
    }
}
