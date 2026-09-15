using System.Collections.Concurrent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.Models;
using SpaceCore.Services;
using SpaceCore.Services.Domain;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingConfirmationDTO>
{
    // Серіалізує перевірку перетину та вставку бронювання по одному зала в межах процесу,
    // щоб два одночасні запити на той самий зал не проскочили обидва повз AnyAsync-перевірку.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> HallLocks = new();

    private readonly AppDbContext _context;
    private readonly ICalculatePriceService _priceService;
    private readonly IHallService _hallService;

    public CreateBookingCommandHandler(AppDbContext context, ICalculatePriceService priceService, IHallService hallService)
    {
        _context = context;
        _priceService = priceService;
        _hallService = hallService;
    }

    public async Task<BookingConfirmationDTO> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // 1. Перевіряємо та отримуємо зал
        var hall = await _hallService.GetByID(request.HallId, cancellationToken);

        if (hall == null)
        {
            throw new KeyNotFoundException($"Hall with ID {request.HallId} was not found.");
        }

        // Тривалість бронювання має бути додатною
        if (request.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be a positive time span.", nameof(request.Duration));
        }

        // Бронювання заднім числом не має сенсу
        if (request.StartDate < DateTime.Now)
        {
            throw new ArgumentException("StartDate cannot be in the past.", nameof(request.StartDate));
        }

        // Обчислюємо час закінчення бронювання
        var endDate = request.StartDate.Add(request.Duration);

        var hallLock = HallLocks.GetOrAdd(request.HallId, _ => new SemaphoreSlim(1, 1));
        await hallLock.WaitAsync(cancellationToken);
        try
        {
            // Перевіряємо, що зал вільний на цей проміжок часу
            var hasOverlap = await _context.Bookings.AnyAsync(
                b => b.OriginalHallId == request.HallId
                     && !b.Removed
                     && b.StartDate < endDate
                     && b.EndDate > request.StartDate,
                cancellationToken);

            if (hasOverlap)
            {
                throw new InvalidOperationException("The hall is already booked for the requested time range.");
            }

            return await CreateBookingAsync(request, hall, endDate, cancellationToken);
        }
        finally
        {
            hallLock.Release();
        }
    }

    private async Task<BookingConfirmationDTO> CreateBookingAsync(
        CreateBookingCommand request, SpaceCore.DTOs.Hall.GetHallDTO hall, DateTime endDate, CancellationToken cancellationToken)
    {
        // 2. Рахуємо ціну оренди залу через ваш сервіс з урахуванням коефіцієнтів
        decimal hallPrice = _priceService.Calculate(request.StartDate, endDate, hall.Price);

        // 3. Обробляємо додаткові послуги та створюємо їхні історичні зліпки (ServiceFreeze)
        var serviceFreezes = new List<ServiceFreezeEntity>();
        decimal servicesTotalCost = 0m;

        if (request.ServiceIds != null && request.ServiceIds.Any())
        {
            // Послуги мають належати саме до цього залу
            var hallServiceIds = hall.Services.Select(s => s.Id).ToHashSet();
            var unknownServiceIds = request.ServiceIds.Where(id => !hallServiceIds.Contains(id)).ToList();

            if (unknownServiceIds.Any())
            {
                throw new ArgumentException(
                    $"The following services are not available for hall {request.HallId}: {string.Join(", ", unknownServiceIds)}",
                    nameof(request.ServiceIds));
            }

            var services = hall.Services.Where(s => request.ServiceIds.Contains(s.Id));

            foreach (var service in services)
            {
                serviceFreezes.Add(new ServiceFreezeEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalId = service.Id,
                    Name = service.Name,
                    Price = service.Price,
                    FreezeDate = DateTime.UtcNow
                });

                servicesTotalCost += service.Price;
            }
        }

        // Загальна сума (оренда залу з урахуванням коефіцієнтів + послуги)
        decimal finalTotalPrice = hallPrice + servicesTotalCost;

        // 4. Створюємо сутність бронювання
        var booking = new BookingEntity
        {
            Id = Guid.NewGuid(),
            OriginalHallId = hall.Id,
            StartDate = request.StartDate,
            EndDate = endDate,
            PricePerHour = hall.Price, // Зберігаємо ставку на момент броні
            TotalPrice = finalTotalPrice,
            Services = serviceFreezes
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Формуємо вихідне підтвердження для клієнта
        return new BookingConfirmationDTO
        {
            BookingId = booking.Id,
            HallName = hall.Name,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            TotalPrice = booking.TotalPrice,
            Services = serviceFreezes.Select(sf => new BookedServiceDTO
            {
                Name = sf.Name,
                Price = sf.Price
            }).ToList()
        };
    }
}