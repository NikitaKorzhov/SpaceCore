using Microsoft.AspNetCore.Mvc;
using SpaceCore.Services.Domain;

namespace SpaceCore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly ICalculatePriceService _priceService;
    public TestController(ICalculatePriceService priceService)
    {
        _priceService = priceService;
    }
    // GET
    [HttpGet("price")]
    public IActionResult TestPriceCalculation([FromQuery] string time, [FromQuery] decimal price)
    {
        // Перевіряємо, чи передано параметр часу та чи містить він дефіс
        if (string.IsNullOrWhiteSpace(time) || !time.Contains('-'))
        {
            return BadRequest(new { Error = "Invalid time format. Use format like 10:00-14:00" });
        }

        var parts = time.Split('-');
        if (parts.Length != 2 || 
            !TimeSpan.TryParse(parts[0].Trim(), out var startTimeOnly) || 
            !TimeSpan.TryParse(parts[1].Trim(), out var endTimeOnly))
        {
            return BadRequest(new { Error = "Could not parse start or end time." });
        }

        // Формуємо дати на сьогодні (або за замовчуванням) з урахуванням переданих годин/хвилин
        var today = DateTime.Today;
        var start = today.Add(startTimeOnly);
        var end = today.Add(endTimeOnly);

        // Викликаємо метод розрахунку сервісу
        decimal finalPrice = _priceService.Calculate(start, end, price);

        // Виводимо в консоль термінала
        Console.WriteLine($"[DEBUG PRICE TEST] Start: {start:HH:mm}, End: {end:HH:mm}, Base Rate: {price}, Calculated Price: {finalPrice}");

        return Ok(new 
        { 
            TimeRange = time,
            BaseRate = price,
            CalculatedPrice = finalPrice 
        });
    }
}