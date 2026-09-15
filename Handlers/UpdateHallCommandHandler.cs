using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;

using SpaceCore.Models;
using SpaceCore.Services.Domain;

namespace SpaceCore.Handlers;

public record UpdateHallCommand(Guid Id, UpdateHallDTO Dto) : IRequest<bool>;

public class UpdateHallCommandHandler : IRequestHandler<UpdateHallCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly IHallValidationService _hallValidationService;

    public UpdateHallCommandHandler(AppDbContext context, IHallValidationService hallValidationService)
    {
        _context = context;
        _hallValidationService = hallValidationService;
    }

    public async Task<bool> Handle(UpdateHallCommand request, CancellationToken cancellationToken)
    {
        var hallId = request.Id;
        var dto = request.Dto;

        _hallValidationService.ValidatePrice(dto.Price);

        // 1. Fetch the hall along with its currently active services (avoid N+1 via .Include)
        var hall = await _context.Halls
            .Include(h => h.Services)
            .FirstOrDefaultAsync(h => h.Id == hallId && !h.Removed, cancellationToken);

        if (hall == null)
        {
            return false; // Hall not found or removed
        }

        var incomingServices = dto.Services ?? new List<UpdateServiceDTO>();

        // Collect the IDs of services that came in the new request (only those that have an Id)
        var incomingIds = incomingServices
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id.Value)
            .ToHashSet();

        // Every referenced Id must belong to this hall, otherwise it would silently vanish
        // instead of being applied (mirrors the service-ownership check in CreateBookingCommandHandler).
        var hallServiceIds = hall.Services.Select(s => s.Id).ToHashSet();
        var unknownServiceIds = incomingIds.Where(id => !hallServiceIds.Contains(id)).ToList();

        if (unknownServiceIds.Any())
        {
            throw new ArgumentException(
                $"The following services do not belong to hall {hallId}: {string.Join(", ", unknownServiceIds)}",
                nameof(dto.Services));
        }

        // 2. Update the hall's base data (e.g., changing the price to 2500 UAH)
        hall.Name = dto.Name;
        hall.Capacity = dto.Capacity;
        hall.PricePerHour = dto.Price;

        // 3. Soft-delete services that are not present in the incoming array
        foreach (var service in hall.Services)
        {
            if (!incomingIds.Contains(service.Id) && !service.Removed)
            {
                service.Removed = true;
            }
        }

        // 4. Add new services or update existing ones
        foreach (var serviceDto in incomingServices)
        {
            if (serviceDto.Id.HasValue)
            {
                // Look for the existing service within the hall
                var existingService = hall.Services.FirstOrDefault(s => s.Id == serviceDto.Id.Value);
                if (existingService != null)
                {
                    existingService.Name = serviceDto.Name;
                    existingService.Price = serviceDto.Price;
                    existingService.Removed = false; // In case it was marked as removed
                }
            }
            else
            {
                // Adding a new service (e.g., "Sound" priced at 700 UAH)
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

        // 5. Save all changes to the database in a single transaction
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}