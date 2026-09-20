using Domain.Common;
using Domain.Enum;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using Services.Response.Analytics;

namespace Services.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(AppDbContext context, ILogger<AnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync() 
        { 
            var nowUtc = DateTime.UtcNow;

            var startOfYearUtc = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            int diffToMonday = (7 + ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            var startOfWeekUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(-diffToMonday);

            var completeDeals = _context.Deals
                .AsNoTracking()
                .Where(d => d.Status == DealsStatusEnum.Complete);

            var reviewYearRaw = await completeDeals
                .Where(d => d.CloseDate >= startOfYearUtc)
                .SumAsync(d => (long?)d.Value) ?? 0;

            var reviewMonthRaw = await completeDeals
                .Where(d => d.CloseDate >= startOfMonthUtc)
                .SumAsync(d => (long?)d.Value) ?? 0;

            var reviewWeekRaw = await completeDeals
               .Where(d => d.CloseDate >= startOfWeekUtc)
               .SumAsync(d => (long?)d.Value) ?? 0;

            var activeDealsCount = await _context.Deals
                .AsNoTracking()
                .CountAsync(d => d.Status != DealsStatusEnum.Complete && d.Status != DealsStatusEnum.Cancelled);

            var wonDealsThisMonth = await _context.Deals
                .AsNoTracking()
                .CountAsync(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= startOfMonthUtc);

            var lostDealsThisMonth = await _context.Deals
                .AsNoTracking()
                .CountAsync(d => d.Status == DealsStatusEnum.Cancelled && d.CloseDate >= startOfMonthUtc);

            var completedTasksThisMonth = await _context.Tasks
                .AsNoTracking()
                .CountAsync(t => t.Status == TaskStatusEnum.Complete && t.DueAt >= startOfMonthUtc);

            var pendingTasksCount = await _context.Tasks
                .AsNoTracking()
                .CountAsync(t => t.Status != TaskStatusEnum.Complete);

            var overdueTasksCount = await _context.Tasks
                .AsNoTracking()
                .CountAsync(t => t.Status != TaskStatusEnum.Complete && t.DueAt < nowUtc);

            var response = new TeamKpiSummaryResponse
            {
                RevenueThisWeek = Math.Round(reviewWeekRaw / 10000.0m, 2),
                RevenueThisMonth = Math.Round(reviewMonthRaw / 10000.0m, 2),
                RevenueThisYear = Math.Round(reviewYearRaw / 10000.0m, 2),
                ActiveDealsCount = activeDealsCount,
                WonDealsThisMonth = wonDealsThisMonth,
                LostDealsThisMonth = lostDealsThisMonth,
                CompletedTasksThisMonth = completedTasksThisMonth,
                PendingTasksCount = pendingTasksCount,
                OverdueTasksCount = overdueTasksCount
            };

            _logger.LogInformation("Team KPI summary retrieved successfully.");
            return Result<TeamKpiSummaryResponse>.Success(
                message: "Team KPI summary retrieved successfully.",
                data: response,
                statusCode: StatusCodes.Status200OK
                );

        }
    }
}
