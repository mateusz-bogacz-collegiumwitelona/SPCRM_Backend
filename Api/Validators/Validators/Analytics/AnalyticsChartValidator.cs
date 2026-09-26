using Api.Request.Analytics;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Analytics
{
    public class AnalyticsChartValidator : AbstractValidator<AnalyticsChartRequest>
    {
        public AnalyticsChartValidator()
        {
            RuleFor(x => x.Period).ApplyPeriodRules();
            RuleFor(x => x.CurrencyId).ApplyValidGuidRule();
        }
    }
}
