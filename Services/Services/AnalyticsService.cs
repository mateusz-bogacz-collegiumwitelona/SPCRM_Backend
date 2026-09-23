using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Pdf.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors;
using Services.Command.Analytics;
using Services.Command.List;
using Services.Helpers;
using Services.Interfaces;
using Services.Response.Analytics;
using Services.Response.Pdf;

namespace Services.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly IAnalyticsPdfGenerator _pdf;
        private readonly AnalyticsMapper _mapper = new AnalyticsMapper();
        private readonly ICancellationTokenAccessor _ctAccessor;

        private CancellationToken _ct => _ctAccessor.Token;

        private sealed record DatePeriodsContext(
            DateTime NowUtc,
            DateTime StartOfWeekUtc,
            DateTime StartOfMonthUtc,
            DateTime StartOfYearUtc);

        private sealed record BaseKpiMetrics(
            List<CurrencyAmountResponse> RevenueThisWeek,
            List<CurrencyAmountResponse> RevenueThisMonth,
            List<CurrencyAmountResponse> RevenueThisYear,
            int ActiveDealsCount,
            List<CurrencyAmountResponse> ActiveDealsPipelineValue,
            int WonDealsThisMonth,
            int LostDealsThisMonth,
            decimal WinRatePercentageThisMonth,
            int CompletedTasksThisMonth,
            int PendingTasksCount,
            int OverdueTasksCount);

        public AnalyticsService(
            AppDbContext context,
            ILogger<AnalyticsService> logger,
            IAnalyticsPdfGenerator pdf,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _pdf = pdf;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<TeamKpiSummaryResponse>> GetTeamKpiSummaryAsync()
        {

            var metrics = await CalculateKpiMetricsAsync();

            var response = new TeamKpiSummaryResponse
            {
                RevenueThisWeek = metrics.RevenueThisWeek,
                RevenueThisMonth = metrics.RevenueThisMonth,
                RevenueThisYear = metrics.RevenueThisYear,
                ActiveDealsCount = metrics.ActiveDealsCount,
                WonDealsThisMonth = metrics.WonDealsThisMonth,
                LostDealsThisMonth = metrics.LostDealsThisMonth,
                CompletedTasksThisMonth = metrics.CompletedTasksThisMonth,
                PendingTasksCount = metrics.PendingTasksCount,
                OverdueTasksCount = metrics.OverdueTasksCount
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
            var baseQuery = _context.Deals
                .AsNoTracking()
                .Where(d => d.Status == DealsStatusEnum.Complete);

            var chartItems = await BuildRevenueChartAsync(baseQuery, command.Period, command.CurrencyId);

            _logger.LogInformation("Team revenue chart retrieved successfully for period: {Period}.", command.Period.ToString());

            return Result<List<AnalyticsChartMetricResponse>>.Success(
                data: chartItems,
                message: "Chart data retrieved successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<EmployeeKpiSummaryResponse>> GetEmployeeKpiSummaryAsync(Guid employeeId)
        {
            var user = await _context.Users.AsNoTracking()
                .Where(u => u.Id == employeeId && !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email
                })
                .FirstOrDefaultAsync(_ct);

            if (user == null)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} not found.", employeeId);
                return Result<EmployeeKpiSummaryResponse>.Failure(
                    message: "Employee not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            var metrics = await CalculateKpiMetricsAsync(employeeId);

            var response = new EmployeeKpiSummaryResponse
            {
                EmployeeId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                RevenueThisWeek = metrics.RevenueThisWeek,
                RevenueThisMonth = metrics.RevenueThisMonth,
                RevenueThisYear = metrics.RevenueThisYear,
                ActiveDealsCount = metrics.ActiveDealsCount,
                ActiveDealsPipelineValue = metrics.ActiveDealsPipelineValue,
                WonDealsThisMonth = metrics.WonDealsThisMonth,
                LostDealsThisMonth = metrics.LostDealsThisMonth,
                WinRatePercentageThisMonth = metrics.WinRatePercentageThisMonth,
                CompletedTasksThisMonth = metrics.CompletedTasksThisMonth,
                PendingTasksCount = metrics.PendingTasksCount,
                OverdueTasksCount = metrics.OverdueTasksCount
            };

            _logger.LogInformation("Employee KPI summary retrieved successfully for user: {EmployeeId}", employeeId);
            return Result<EmployeeKpiSummaryResponse>.Success(
                data: response,
                message: "Employee KPI summary retrieved successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<List<AnalyticsChartMetricResponse>>> GetEmployeeRevenueChartAsync(Guid employeeId, AnalyticsChartCommand command)
        {
            var user = await _context.Users.AsNoTracking()
                .Where(u => u.Id == employeeId && !u.IsDeleted)
                .AnyAsync(_ct);

            if (!user)
            {
                _logger.LogWarning("Analytics chart requested for non-existing employee: {EmployeeId}", employeeId);
                return Result<List<AnalyticsChartMetricResponse>>.Failure(
                    message: "Employee not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            var baseQuery = _context.Deals
                 .AsNoTracking()
                 .Where(d => d.OwnerId == employeeId && d.Status == DealsStatusEnum.Complete);

            var chartItems = await BuildRevenueChartAsync(baseQuery, command.Period);

            _logger.LogInformation("Employee revenue chart retrieved successfully for user: {EmployeeId}, Period: {Period}.", employeeId, command.Period);

            return Result<List<AnalyticsChartMetricResponse>>.Success(
                data: chartItems,
                message: "Employee chart data retrieved successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<PagedResult<LeaderboardItemResponse>>> GetTeamLeaderboardAsync(PaggedCommand command)
        {
            var periods = GetDatePeriods();

            return await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .OrderByDescending(u => u.Deals.Count(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc))
                .ThenBy(u => u.LastName)
                .Select(u => new LeaderboardItemResponse
                {
                    EmployeeId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email ?? string.Empty,

                    WonDealsThisMonth = u.Deals
                        .Count(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc),

                    ActiveDealsCount = u.Deals
                        .Count(d => d.Status != DealsStatusEnum.Complete && d.Status != DealsStatusEnum.Cancelled),

                    WinRatePercentageThisMonth = (u.Deals.Count(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc) +
                                                 u.Deals.Count(d => d.Status == DealsStatusEnum.Cancelled && d.CloseDate >= periods.StartOfMonthUtc)) > 0
                        ? Math.Round(
                            (decimal)u.Deals.Count(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc) /
                            (u.Deals.Count(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc) +
                             u.Deals.Count(d => d.Status == DealsStatusEnum.Cancelled && d.CloseDate >= periods.StartOfMonthUtc)) * 100m,
                            BusinessConstants.DefaultPercentageDecimalPlaces)
                        : 0m,

                    RevenueThisMonth = u.Deals
                        .Where(d => d.Status == DealsStatusEnum.Complete && d.CloseDate >= periods.StartOfMonthUtc)
                        .GroupBy(d => new { d.Currency.Code, d.Currency.DecimalPlaces })
                        .Select(g => new CurrencyAmountResponse
                        {
                            CurrencyCode = g.Key.Code,
                            DecimalPlaces = g.Key.DecimalPlaces,
                            Amount = (decimal)(g.Sum(d => (long?)d.Value) ?? 0) / BusinessConstants.CurrencyScaleFactor
                        })
                        .ToList()
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "team_leaderboard", _ct);
        }

        public async Task<Result<PdfFileResponse>> GenerateEmployeeReportPdfAsync(Guid employeeId, AnalyticsChartCommand chartCommand)
        {
            var summaryResponse = await GetEmployeeKpiSummaryAsync(employeeId);

            if (!summaryResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to generate PDF report for employee {EmployeeId}: {ErrorMessage}", employeeId, summaryResponse.Message);
                return Result<PdfFileResponse>.Failure(
                    message: summaryResponse.Message ?? "Failed to generate PDF report",
                    statusCode: summaryResponse.StatusCode,
                    errorCode: summaryResponse.ErrorCode ?? ErrorCodes.InternalError
                );
            }

            var chartResponse = await GetEmployeeRevenueChartAsync(employeeId, chartCommand);

            if (!chartResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to generate PDF report for employee {EmployeeId}: {ErrorMessage}", employeeId, chartResponse.Message);
                return Result<PdfFileResponse>.Failure(
                    message: chartResponse.Message ?? "Failed to generate PDF report",
                    statusCode: chartResponse.StatusCode,
                    errorCode: chartResponse.ErrorCode ?? ErrorCodes.InternalError
                );
            }

            var summary = summaryResponse.Data!;
            var historyMetrics = chartResponse.Data ?? new List<AnalyticsChartMetricResponse>();

            var reportModel = _mapper.MapToReportModel(
                 summaryResponse.Data!,
                 chartResponse.Data ?? new List<AnalyticsChartMetricResponse>(),
                 chartCommand.Period);


            byte[] pdfBytes = _pdf.GenerateEmployeeReportPdf(reportModel);

            var safeLastName = summary.LastName.Replace(" ", "_");
            var fileName = $"Raport_{safeLastName}_{chartCommand.Period}_{DateTime.UtcNow:yyyyMMdd}.pdf";

            var response = new PdfFileResponse
            {
                FileContents = pdfBytes,
                ContentType = "application/pdf",
                FileName = fileName
            };

            _logger.LogInformation("Analytics report PDF generated successfully for employee: {EmployeeId}, Period: {Period}", employeeId, chartCommand.Period);
            return Result<PdfFileResponse>.Success(
               data: response,
               message: "Report generated successfully.",
               statusCode: StatusCodes.Status200OK);
        }


        public async Task<Result<PdfFileResponse>> GenerateTeamReportPdfAsync(AnalyticsChartCommand chartCommand)
        {
            var summaryResponse = await GetTeamKpiSummaryAsync();
            var chartResponse = await GetTeamRevenueChartAsync(chartCommand);

            var leaderboardResponse = await GetTeamLeaderboardAsync(new PaggedCommand
            {
                PageNumber = 1,
                PageSize = 5
            });

            var summary = summaryResponse.Data!;
            var historyMetrics = chartResponse.Data ?? new List<AnalyticsChartMetricResponse>();
            var topPerformers = leaderboardResponse.Data?.Items.ToList() ?? new List<LeaderboardItemResponse>();

            var reportModel = _mapper.MapToTeamReportModel(
                summary,
                historyMetrics,
                topPerformers,
                chartCommand.Period);

            byte[] pdfBytes = _pdf.GenerateTeamReportPdf(reportModel);

            var fileName = $"Raport_Zespolu_{chartCommand.Period}_{DateTime.UtcNow:yyyyMMdd}.pdf";

            var response = new PdfFileResponse
            {
                FileContents = pdfBytes,
                ContentType = "application/pdf",
                FileName = fileName
            };

            _logger.LogInformation("Team analytics report PDF generated successfully. Period: {Period}", chartCommand.Period);
            return Result<PdfFileResponse>.Success(
                data: response,
                message: "Team report generated successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private async Task<List<AnalyticsChartMetricResponse>> BuildRevenueChartAsync(IQueryable<Deal> completeDealsQuery, AnalyticsPeriodEnum period, Guid? currencyId = null)
        {
            if (currencyId.HasValue)
            {
                completeDealsQuery = completeDealsQuery.Where(d => d.CurrencyId == currencyId);

                if (completeDealsQuery == null)
                {
                    _logger.LogError("Currency with id {CurrencyId} dont exist in deal set.", currencyId);
                    throw new InvalidOperationException($"Currency with id {currencyId} dont exist in deal set.");
                } 
            }

            var nowUtc = DateTime.UtcNow;
            var chartItems = new List<AnalyticsChartMetricResponse>();

            switch (period)
            {
                default:
                case AnalyticsPeriodEnum.CurrentMonth:
                    {
                        var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        var dealsInMonth = await completeDealsQuery
                            .Where(d => d.CloseDate >= startOfMonth && d.CloseDate <= nowUtc)
                            .Select(d => new
                            {
                                d.CloseDate,
                                d.Value,
                                CurrencyCode = d.Currency.Code,
                                DecimalPlaces = d.Currency.DecimalPlaces
                            })
                            .ToListAsync(_ct);

                        int daysInMonth = DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month);
                        for (int week = 1; week <= 4; week++)
                        {
                            int startDay = (week - 1) * 7 + 1;
                            int endDay = week == 4 ? daysInMonth : week * 7;
                            var weekDeals = dealsInMonth
                                .Where(d => d.CloseDate.Day >= startDay && d.CloseDate.Day <= endDay)
                                .ToList();

                            var revenues = weekDeals
                                .GroupBy(d => new { d.CurrencyCode, d.DecimalPlaces })
                                .Select(g => new CurrencyAmountResponse
                                {
                                    CurrencyCode = g.Key.CurrencyCode,
                                    DecimalPlaces = g.Key.DecimalPlaces,
                                    Amount = Math.Round((decimal)g.Sum(d => d.Value) / BusinessConstants.CurrencyScaleFactor, g.Key.DecimalPlaces)
                                })
                                .ToList();

                            chartItems.Add(new AnalyticsChartMetricResponse
                            {
                                Label = $"Dni {startDay}-{endDay}",
                                Revenue = revenues,
                                DealsWonCount = weekDeals.Count
                            });
                        }
                        break;
                    }

                case AnalyticsPeriodEnum.HalfYear:
                    {
                        var sixMonthsAgo = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
                        var halfYearDeals = await completeDealsQuery
                            .Where(d => d.CloseDate >= sixMonthsAgo)
                            .GroupBy(d => new
                            {
                                d.CloseDate.Year,
                                d.CloseDate.Month,
                                CurrencyCode = d.Currency.Code,
                                DecimalPlaces = d.Currency.DecimalPlaces
                            })
                            .Select(g => new
                            {
                                g.Key.Year,
                                g.Key.Month,
                                g.Key.CurrencyCode,
                                g.Key.DecimalPlaces,
                                RevenueRaw = g.Sum(x => (long?)x.Value) ?? 0,
                                Count = g.Count()
                            })
                            .ToListAsync(_ct);

                        for (int i = 5; i >= 0; i--)
                        {
                            var target = nowUtc.AddMonths(-i);
                            var matchingDeals = halfYearDeals
                                .Where(d => d.Year == target.Year && d.Month == target.Month)
                                .ToList();

                            var revenues = matchingDeals
                                .Select(m => new CurrencyAmountResponse
                                {
                                    CurrencyCode = m.CurrencyCode,
                                    DecimalPlaces = m.DecimalPlaces,
                                    Amount = Math.Round((decimal)m.RevenueRaw / BusinessConstants.CurrencyScaleFactor, m.DecimalPlaces)
                                })
                                .ToList();

                            chartItems.Add(new AnalyticsChartMetricResponse
                            {
                                Label = target.ToString("MMM yyyy", BusinessConstants.DefaultCultureCode),
                                Revenue = revenues,
                                DealsWonCount = matchingDeals.Sum(m => m.Count)
                            });
                        }
                        break;
                    }

                case AnalyticsPeriodEnum.CurrentYear:
                    {
                        var startOfYear = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var yearDeals = await completeDealsQuery
                            .Where(d => d.CloseDate >= startOfYear)
                            .GroupBy(d => new
                            {
                                Month = d.CloseDate.Month,
                                CurrencyCode = d.Currency.Code,
                                DecimalPlaces = d.Currency.DecimalPlaces
                            })
                            .Select(g => new
                            {
                                g.Key.Month,
                                g.Key.CurrencyCode,
                                g.Key.DecimalPlaces,
                                RevenueRaw = g.Sum(x => (long?)x.Value) ?? 0,
                                Count = g.Count()
                            })
                            .ToListAsync(_ct);

                        for (int m = 1; m <= 12; m++)
                        {
                            var matchingDeals = yearDeals
                                .Where(d => d.Month == m)
                                .ToList();

                            var monthDate = new DateTime(nowUtc.Year, m, 1);
                            var rawMonthName = monthDate.ToString("MMMM", BusinessConstants.DefaultCultureCode);
                            var capitalizedMonth = char.ToUpper(rawMonthName[0], BusinessConstants.DefaultCultureCode) + rawMonthName[1..];

                            var revenues = matchingDeals
                                .Select(d => new CurrencyAmountResponse
                                {
                                    CurrencyCode = d.CurrencyCode,
                                    DecimalPlaces = d.DecimalPlaces,
                                    Amount = Math.Round((decimal)d.RevenueRaw / BusinessConstants.CurrencyScaleFactor, d.DecimalPlaces)
                                })
                                .ToList();

                            chartItems.Add(new AnalyticsChartMetricResponse
                            {
                                Label = capitalizedMonth,
                                Revenue = revenues,
                                DealsWonCount = matchingDeals.Sum(d => d.Count)
                            });
                        }
                        break;
                    }
            }

            return chartItems;
        }

        private static DatePeriodsContext GetDatePeriods()
        {
            var nowUtc = DateTime.UtcNow;
            var startOfYearUtc = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            int diffToMonday = (7 + ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            var startOfWeekUtc = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(-diffToMonday);

            return new DatePeriodsContext(nowUtc, startOfWeekUtc, startOfMonthUtc, startOfYearUtc);
        }

        private async Task<BaseKpiMetrics> CalculateKpiMetricsAsync(Guid? employeeId = null)
        {
            var periods = GetDatePeriods();

            var completeDealsQuery = _context.Deals
                .AsNoTracking()
                .Where(d => d.Status == DealsStatusEnum.Complete);

            if (employeeId.HasValue)
            {
                completeDealsQuery = completeDealsQuery.Where(d => d.OwnerId == employeeId.Value);
            }

            var revYear = await CalculateGroupedRevenueAsync(completeDealsQuery.Where(d => d.CloseDate >= periods.StartOfYearUtc));

            var revMonth = await CalculateGroupedRevenueAsync(completeDealsQuery.Where(d => d.CloseDate >= periods.StartOfMonthUtc));

            var revWeek = await CalculateGroupedRevenueAsync(completeDealsQuery.Where(d => d.CloseDate >= periods.StartOfWeekUtc));

            var activeDealsQuery = _context.Deals
                .AsNoTracking()
                .Where(d => d.Status != DealsStatusEnum.Complete && d.Status != DealsStatusEnum.Cancelled);

            if (employeeId.HasValue)
            {
                activeDealsQuery = activeDealsQuery.Where(d => d.OwnerId == employeeId.Value);
            }
            var activeDealsPipeline = await CalculateGroupedRevenueAsync(activeDealsQuery);

            var activeDealsCount = await activeDealsQuery.CountAsync(_ct);
            var activeDealsValueRaw = await activeDealsQuery.SumAsync(d => (long?)d.Value, _ct) ?? 0;

            var baseMonthDeals = _context.Deals
                .AsNoTracking()
                .Where(d => d.CloseDate >= periods.StartOfMonthUtc);

            if (employeeId.HasValue)
            {
                baseMonthDeals = baseMonthDeals.Where(d => d.OwnerId == employeeId.Value);
            }

            var wonDealsThisMonth = await baseMonthDeals
                .CountAsync(d => d.Status == DealsStatusEnum.Complete);

            var lostDealsThisMonth = await baseMonthDeals
                .CountAsync(d => d.Status == DealsStatusEnum.Cancelled);

            int closedDealsTotal = wonDealsThisMonth + lostDealsThisMonth;
            decimal winRate = closedDealsTotal > 0
                ? Math.Round(((decimal)wonDealsThisMonth / closedDealsTotal) * 100m, BusinessConstants.DefaultPercentageDecimalPlaces)
                : 0m;

            var tasksQuery = _context.Tasks.AsNoTracking();

            if (employeeId.HasValue)
            {
                tasksQuery = tasksQuery.Where(t => t.AssignedToId == employeeId.Value);
            }

            var completedTasksThisMonth = await tasksQuery
                .CountAsync(t => t.Status == TaskStatusEnum.Complete && t.DueAt >= periods.StartOfMonthUtc, _ct);

            var pendingTasksCount = await tasksQuery
                .CountAsync(t => t.Status != TaskStatusEnum.Complete, _ct);

            var overdueTasksCount = await tasksQuery
                .CountAsync(t => t.Status != TaskStatusEnum.Complete && t.DueAt < periods.NowUtc, _ct);

            return new BaseKpiMetrics(
                RevenueThisWeek: revWeek,
                RevenueThisMonth: revMonth,
                RevenueThisYear: revYear,
                ActiveDealsCount: activeDealsCount,
                ActiveDealsPipelineValue: activeDealsPipeline,
                WonDealsThisMonth: wonDealsThisMonth,
                LostDealsThisMonth: lostDealsThisMonth,
                WinRatePercentageThisMonth: winRate,
                CompletedTasksThisMonth: completedTasksThisMonth,
                PendingTasksCount: pendingTasksCount,
                OverdueTasksCount: overdueTasksCount
            );
        }

        private async Task<List<CurrencyAmountResponse>> CalculateGroupedRevenueAsync(IQueryable<Deal> query)
        {
            var raw = await query
                .GroupBy(d => new { d.Currency.Code, d.Currency.DecimalPlaces })
                .Select(g => new
                {
                    g.Key.Code,
                    g.Key.DecimalPlaces,
                    TotalRaw = g.Sum(d => (long?)d.Value) ?? 0
                })
                .ToListAsync(_ct);

            return raw.Select(r => new CurrencyAmountResponse
            {
                CurrencyCode = r.Code,
                DecimalPlaces = r.DecimalPlaces,
                Amount = Math.Round(r.TotalRaw / BusinessConstants.CurrencyScaleFactor, r.DecimalPlaces)
            }).ToList();
        }
    }
}

