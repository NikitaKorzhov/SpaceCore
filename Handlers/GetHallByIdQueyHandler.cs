using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.DTOs.Hall;

namespace SpaceCore.Handlers;

public record GetHallByIdQuery(Guid Id) : IRequest<GetHallDTO?>;

public class GetHallByIdQueryHandler : IRequestHandler<GetHallByIdQuery, GetHallDTO?>
{
    private readonly AppDbContext _context;

    public GetHallByIdQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GetHallDTO?> Handle(GetHallByIdQuery request, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls
            .Include(h => h.Services)
            .FirstOrDefaultAsync(h => h.Id == request.Id && h.Removed == false, cancellationToken);

        if (hall == null)
        {
            return null;
        }

        return new GetHallDTO
        {
            Id = hall.Id,
            Name = hall.Name,
            Capacity = hall.Capacity,
            Price = hall.PricePerHour,
            Removed = hall.Removed,
            Services = hall.Services
                .Where(s => !s.Removed) // Фільтруємо послуги, залишаючи лише активні
                .Select(s => new GetServiceDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price
                }).ToList()
        };
    }
}