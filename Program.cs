using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.Services;
using SpaceCore.Services.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IHallService, HallService>();
// Реєстрація правил ціноутворення та самого сервісу
builder.Services.AddTransient<ICalculatePriceService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    
    // Тепер конфігурація без проблем заповнить список об'єктів
    var rawRules = configuration.GetSection("PricingRules").Get<List<PricingRuleConfig>>() ?? new();

    return new CalculatePriceService(rawRules);
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();


