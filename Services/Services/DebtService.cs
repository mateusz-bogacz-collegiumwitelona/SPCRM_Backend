using Domain.Common;
using DTO.Response;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

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
    }
}
