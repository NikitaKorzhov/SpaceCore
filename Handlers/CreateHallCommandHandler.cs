using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;
using SpaceCore.Models;

namespace SpaceCore.Handlers;

public record CreateHallCommand(CreateHallDTO Dto) : IRequest<GetHallDTO>;

public class CreateHallCommandHandler : IRequestHandler<CreateHallCommand, GetHallDTO>
{
    private readonly AppDbContext _context;

    public CreateHallCommandHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GetHallDTO> Handle(CreateHallCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        
        // Захист від null, якщо масив послуг не передали
        var servicesDto = dto.Services ?? new List<CreateServiceDTO>();

        var newHall = new HallEntity
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Capacity = dto.Capacity,
            PricePerHour = dto.Price,
            Services = new List<ServiceEntity>()
        };

        // Для кожної послуги з DTO створюємо окрему сутність із ціною з поточного запиту
        foreach (var serviceDto in servicesDto)
        {
            var newService = new ServiceEntity
            {
                Id = Guid.NewGuid(),
                Name = serviceDto.Name,
                Price = serviceDto.Price, // Беремо актуальну ціну з поточного запиту
                Removed = false
            };

            _context.Services.Add(newService);
            newHall.Services.Add(newService);
        }

        _context.Halls.Add(newHall);
        await _context.SaveChangesAsync(cancellationToken);

        return new GetHallDTO
        {
            Id = newHall.Id,
            Name = newHall.Name,
            Capacity = newHall.Capacity,
            Price = newHall.PricePerHour,
            Removed = newHall.Removed,
            Services = newHall.Services.Select(s => new GetServiceDTO
            {
                Id = s.Id,
                Name = s.Name,
                Price = s.Price
            }).ToList()
        };
    }
}