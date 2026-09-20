using Domain.Common;
using Domain.Enum;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Analytics;
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

        public async Task<Result<List<AnalyticsChartMetricResponse>>> GetTeamRevenueChartAsync(AnalyticsChartCommand command)
        {
            var nowUtc = DateTime.UtcNow;
            var chartItems = new List<AnalyticsChartMetricResponse>();

            switch (command.Period)
            {
                default:
                case AnalyticsPeriodEnum.CurrentMonth:
                {
                    var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var dealsInMonth = await _context.Deals
                        .AsNoTracking()
                        .Where(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= startOfMonth && d.CloseDate <= nowUtc)
                        .Select(d => new { d.CloseDate, d.Value })
                        .ToListAsync();

                int daysInMonth = DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month);
                    for (int week = 1; week <= 4; week++)
                    {
                        int startDay = (week - 1) * 7 + 1;
                        int endDay = week == 4 ? daysInMonth : week * 7;
                        var weekDeals = dealsInMonth
                            .Where(d => d.CloseDate.Day >= startDay && d.CloseDate.Day <= endDay)
                            .ToList();

                        chartItems.Add(new AnalyticsChartMetricResponse
                        {
                            Label = $"Dni {startDay}-{endDay}",
                            Revenue = Math.Round((weekDeals.Sum(d => (long?)d.Value) ?? 0) / 10000.0m, 2),
                            DealsWonCount = weekDeals.Count
                        });
                    }
                    break;
                }

                case AnalyticsPeriodEnum.HalfYear:
                {
                    var sixMonthsAgo = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
                    var halfYearDeals = await _context.Deals
                        .AsNoTracking()
                        .Where(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= sixMonthsAgo)
                        .GroupBy(d => new { d.CloseDate.Year, d.CloseDate.Month })
                        .Select(g => new
                        {
                            g.Key.Year,
                            g.Key.Month,
                            RevenueRaw = g.Sum(x => (long?)x.Value) ?? 0,
                            Count = g.Count()
                        }).ToListAsync();

                    for (int i = 5; i >= 0; i--)
                    {
                        var target = nowUtc.AddMonths(-i);
                        var found = halfYearDeals.FirstOrDefault(d => d.Year == target.Year && d.Month == target.Month);

                        chartItems.Add(new AnalyticsChartMetricResponse
                        {
                            Label = target.ToString("MMM yyyy"),
                            Revenue = found != null ? Math.Round(found.RevenueRaw / 10000.0m, 2) : 0m,
                            DealsWonCount = found?.Count ?? 0
                        });
                        }
                    break;
                }

                case AnalyticsPeriodEnum.CurrentYear:
                {
                    var startOfYear = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var yearDeals = await _context.Deals
                        .AsNoTracking()
                        .Where(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= startOfYear)
                        .GroupBy(d => d.CloseDate.Month)
                        .Select(g => new
                        {
                            Month = g.Key,
                            RevenueRaw = g.Sum(x => (long?)x.Value) ?? 0,
                            Count = g.Count()
                        }).ToListAsync();

                    for (int m = 1; m <= 12; m++)
                    {
                        var found = yearDeals.FirstOrDefault(d => d.Month == m);
                        var monthDate = new DateTime(nowUtc.Year, m, 1);

                        chartItems.Add(new AnalyticsChartMetricResponse
                        {
                            Label = monthDate.ToString("MMM"),
                            Revenue = found != null ? Math.Round(found.RevenueRaw / 10000.0m, 2) : 0m,
                            DealsWonCount = found?.Count ?? 0
                        });
                        }
                    break;
                }
            }

            _logger.LogInformation("Team revenue chart retrieved successfully for period: {Period}.", command.Period.ToString());

            return Result<List<AnalyticsChartMetricResponse>>.Success(
                data: chartItems,
                message: "Chart data retrieved successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
