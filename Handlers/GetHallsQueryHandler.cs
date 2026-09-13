using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;

namespace SpaceCore.Handlers;

public record GetHallsQuery : IRequest<IEnumerable<GetHallDTO>>;

public class GetHallsQueryHandler : IRequestHandler<GetHallsQuery, IEnumerable<GetHallDTO>>
{
    private readonly AppDbContext _context;

    public GetHallsQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetHallDTO>> Handle(GetHallsQuery request, CancellationToken cancellationToken)
    {
        var halls = await _context.Halls
            .Where(h => h.Removed == false)
            .Include(h => h.Services)
            .ToListAsync(cancellationToken);

        return halls.Select(h => new GetHallDTO
        {
            Id = h.Id,
            Name = h.Name,
            Capacity = h.Capacity,
            Price = h.PricePerHour,
            Removed = h.Removed,
            Services = h.Services
                .Where(s => !s.Removed) // Фільтруємо видалені послуги для кожного залу
                .Select(s => new GetServiceDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price
                }).ToList()
        }).ToList();
    }
}