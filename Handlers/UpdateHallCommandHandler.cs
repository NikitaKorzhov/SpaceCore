using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;

using SpaceCore.Models;

namespace SpaceCore.Handlers;

public record UpdateHallCommand(Guid Id, UpdateHallDTO Dto) : IRequest<bool>;

public class UpdateHallCommandHandler : IRequestHandler<UpdateHallCommand, bool>
{
    private readonly AppDbContext _context;

    public UpdateHallCommandHandler(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<bool> Handle(UpdateHallCommand request, CancellationToken cancellationToken)
    {
        var hallId = request.Id;
        var dto = request.Dto;

        // 1. Отримуємо зал разом із поточними активними послугами (запобігаємо N+1 через .Include)
        var hall = await _context.Halls
            .Include(h => h.Services)
            .FirstOrDefaultAsync(h => h.Id == hallId && !h.Removed, cancellationToken);

        if (hall == null)
        {
            return false; // Зал не знайдено або видалено
        }

        // 2. Оновлюємо базові дані зали (наприклад, зміна вартості до 2500 грн)
        hall.Name = dto.Name;
        hall.Capacity = dto.Capacity;
        hall.PricePerHour = dto.Price;

        var incomingServices = dto.Services ?? new List<UpdateServiceDTO>();

        // Збираємо ID послуг, які прийшли у новому запиті (тільки ті, що мають Id)
        var incomingIds = incomingServices
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id.Value)
            .ToHashSet();

        // 3. М'яке видалення послуг, яких немає у вхідному масиві
        foreach (var service in hall.Services)
        {
            if (!incomingIds.Contains(service.Id) && !service.Removed)
            {
                service.Removed = true;
            }
        }

        // 4. Додавання нових або оновлення існуючих послуг
        foreach (var serviceDto in incomingServices)
        {
            if (serviceDto.Id.HasValue)
            {
                // Шукаємо існуючу послугу в межах зали
                var existingService = hall.Services.FirstOrDefault(s => s.Id == serviceDto.Id.Value);
                if (existingService != null)
                {
                    existingService.Name = serviceDto.Name;
                    existingService.Price = serviceDto.Price;
                    existingService.Removed = false; // На випадок, якщо вона була позначена як видалена
                }
            }
            else
            {
                // Додавання нової послуги (наприклад, "Звук" вартість 700 грн)
                var newService = new ServiceEntity
                {
                    Id = Guid.NewGuid(),
                    Name = serviceDto.Name,
                    Price = serviceDto.Price,
                    Removed = false
                };

                _context.Services.Add(newService);
                hall.Services.Add(newService);
            }
        }

        // 5. Зберігаємо всі зміни в базі єдиною транзакцією
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}