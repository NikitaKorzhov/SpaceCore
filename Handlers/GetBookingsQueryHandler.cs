using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;

namespace SpaceCore.Handlers;

public record GetBookingsQuery : IRequest<IEnumerable<GetBookingDTO>>;

public class GetBookingsQueryHandler : IRequestHandler<GetBookingsQuery, IEnumerable<GetBookingDTO>>
{
    private readonly AppDbContext _context;

    public GetBookingsQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetBookingDTO>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var bookings = await _context.Bookings
            .Where(b => !b.Removed)
            .Include(b => b.Services)
            .ToListAsync(cancellationToken);

        // Назви залів підтягуємо окремо, включно з уже видаленими —
        // щоб історичні бронювання не втрачали назву зали.
        var hallNames = await _context.Halls
            .Select(h => new { h.Id, h.Name })
            .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken);

        return bookings.Select(b => new GetBookingDTO
        {
            Id = b.Id,
            HallId = b.OriginalHallId,
            HallName = hallNames.GetValueOrDefault(b.OriginalHallId, string.Empty),
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            TotalPrice = b.TotalPrice,
            Services = b.Services
                .Where(s => !s.Removed)
                .Select(s => new BookedServiceDTO
                {
                    Name = s.Name,
                    Price = s.Price
                }).ToList()
        }).ToList();
    }
}
