using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;
using SpaceCore.Models;
using SpaceCore.Services.Domain;

namespace SpaceCore.Handlers;

public record CreateHallCommand(CreateHallDTO Dto) : IRequest<GetHallDTO>;

public class CreateHallCommandHandler : IRequestHandler<CreateHallCommand, GetHallDTO>
{
    private readonly AppDbContext _context;
    private readonly IHallValidationService _hallValidationService;

    public CreateHallCommandHandler(AppDbContext context, IHallValidationService hallValidationService)
    {
        _context = context;
        _hallValidationService = hallValidationService;
    }

    public async Task<GetHallDTO> Handle(CreateHallCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        _hallValidationService.ValidatePrice(dto.Price);

        // Guard against null if the services array was not provided
        var servicesDto = dto.Services ?? new List<CreateServiceDTO>();

        var newHall = new HallEntity
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Capacity = dto.Capacity,
            PricePerHour = dto.Price,
            Services = new List<ServiceEntity>()
        };

        // For each service from the DTO, create a separate entity with the price from the current request
        foreach (var serviceDto in servicesDto)
        {
            var newService = new ServiceEntity
            {
                Id = Guid.NewGuid(),
                Name = serviceDto.Name,
                Price = serviceDto.Price, // Take the current price from the current request
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