namespace SpaceCore.Services.Domain;

public class HallPriceRulesConfig
{
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; } = decimal.MaxValue;
}

public interface IHallValidationService
{
    void ValidatePrice(decimal price);
}

public class HallValidationService : IHallValidationService
{
    private readonly HallPriceRulesConfig _rules;

    public HallValidationService(HallPriceRulesConfig rules)
    {
        _rules = rules;
    }

    public void ValidatePrice(decimal price)
    {
        if (price < _rules.MinPrice || price > _rules.MaxPrice)
        {
            throw new ArgumentException(
                $"Price must be between {_rules.MinPrice} and {_rules.MaxPrice}.",
                nameof(price));
        }
    }
}
