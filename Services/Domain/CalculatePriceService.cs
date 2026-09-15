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

// Конструктор приймає набір правил із проміжками часу та множниками
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

    public decimal Calculate(DateTime startDate, DateTime endDate, decimal baseHourlyRate)
    {
        if (endDate <= startDate) return 0m;

        decimal totalCost = 0m;
        var current = startDate;

        while (current < endDate)
        {
            // 1. Знаходимо найближчу межу годин або закінчення бронювання за замовчуванням (плюс 1 година)
            var nextBoundary = current.AddHours(1);

            // 2. Перевіряємо, чи є межі правил (StartTime / EndTime), які перетинаються всередині цієї години
            foreach (var rule in _rules)
            {
                var ruleStartToday = current.Date.Add(rule.StartTime);
                var ruleEndToday = current.Date.Add(rule.EndTime);

                // Якщо межа правила знаходиться між поточним часом і кінцем поточного годинного інтервалу
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

            // Шукаємо правило для поточного сегмента
            var applicableRule = _rules.FirstOrDefault(r => timeOfDay >= r.StartTime && timeOfDay < r.EndTime);
            decimal multiplier = applicableRule?.Multiplier ?? 1.0m;
        
            totalCost += baseHourlyRate * multiplier * durationHours;
            current = nextBoundary;
        }

        return Math.Round(totalCost, 2);
    }
}