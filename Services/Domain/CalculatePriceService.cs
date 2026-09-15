namespace SpaceCore.Services.Domain;

public record TimeSlotRateRule(
    TimeSpan StartTime, 
    TimeSpan EndTime, 
    decimal Multiplier
);
public class PricingRuleConfig
{
    public string Range { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
}

public interface ICalculatePriceService
{
    decimal Calculate(DateTime startDate, DateTime endDate, decimal baseHourlyRate);
}

public class CalculatePriceService : ICalculatePriceService
{
    private readonly List<TimeSlotRateRule> _rules;

// The constructor takes a set of rules with time ranges and multipliers
    public CalculatePriceService(IEnumerable<PricingRuleConfig> rawRules)
    {
        _rules = new List<TimeSlotRateRule>();

        foreach (var rule in rawRules)
        {
            var parts = rule.Range.Split('-');
            if (parts.Length == 2 &&
                TimeSpan.TryParse(parts[0].Trim(), out var startTime) &&
                TimeSpan.TryParse(parts[1].Trim(), out var endTime))
            {
                _rules.Add(new TimeSlotRateRule(startTime, endTime, rule.Multiplier));
            }
        }
    }

    // Walks the booking interval segment by segment instead of a flat (hours * rate) calculation,
    // because the hourly rate can change mid-booking whenever a pricing rule's start/end time falls
    // inside the interval (e.g. a booking from 20:00 to 23:00 with a "night" rule starting at 22:00
    // must be billed at the day rate for 20:00-22:00 and the night rate for 22:00-23:00).
    // Each loop iteration advances "current" to the next point where either a full hour has passed
    // or a rule boundary is crossed, whichever comes first, and prices only that sub-segment.
    public decimal Calculate(DateTime startDate, DateTime endDate, decimal baseHourlyRate)
    {
        if (endDate <= startDate) return 0m;

        decimal totalCost = 0m;
        var current = startDate;

        while (current < endDate)
        {
            // 1. Find the nearest hour boundary, defaulting to the booking end (plus 1 hour)
            var nextBoundary = current.AddHours(1);

            // 2. Check whether any rule boundaries (StartTime / EndTime) fall within this hour
            foreach (var rule in _rules)
            {
                var ruleStartToday = current.Date.Add(rule.StartTime);
                var ruleEndToday = current.Date.Add(rule.EndTime);

                // If the rule boundary falls between the current time and the end of the current hour interval
                if (ruleStartToday > current && ruleStartToday < nextBoundary)
                {
                    nextBoundary = ruleStartToday;
                }
                if (ruleEndToday > current && ruleEndToday < nextBoundary)
                {
                    nextBoundary = ruleEndToday;
                }
            }

            if (nextBoundary > endDate)
            {
                nextBoundary = endDate;
            }

            var durationHours = (decimal)(nextBoundary - current).TotalHours;
            var timeOfDay = current.TimeOfDay;

            // Look for the rule applicable to the current segment.
            // Because the segment boundaries above already stop at every rule's StartTime/EndTime,
            // by this point "timeOfDay" can only fall inside at most one rule's range in a well-formed
            // (non-overlapping) configuration; FirstOrDefault just picks the first match if rules do overlap.
            var applicableRule = _rules.FirstOrDefault(r => timeOfDay >= r.StartTime && timeOfDay < r.EndTime);
            // No matching rule (e.g. a gap between configured ranges) falls back to the base rate (x1).
            decimal multiplier = applicableRule?.Multiplier ?? 1.0m;
        
            totalCost += baseHourlyRate * multiplier * durationHours;
            current = nextBoundary;
        }

        return Math.Round(totalCost, 2);
    }
}