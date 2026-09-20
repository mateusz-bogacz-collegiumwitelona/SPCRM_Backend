using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Response.Analytics
{
    public record AnalyticsChartMetricResponse
    {
        public required string Label { get; init; }
        public decimal Revenue { get; init; }
        public int DealsWonCount { get; init; }
    }
}
