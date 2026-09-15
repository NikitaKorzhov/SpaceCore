using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Common;
using SpaceCore.Data;
using SpaceCore.DTOs.Hall;

namespace SpaceCore.Handlers;

// StartDate/EndDate приходять рядком у форматі "dd-MM-yyyy HH:mm" (query string не проходить
// через JsonConverter, тому парсимо вручну тим самим форматом, що й тіло запиту бронювання).
public record SearchAvailableHallsQuery(string StartDate, string EndDate, int Capacity)
    : IRequest<IEnumerable<GetHallDTO>>;

public class SearchAvailableHallsQueryHandler : IRequestHandler<SearchAvailableHallsQuery, IEnumerable<GetHallDTO>>
{
    private readonly AppDbContext _context;

    public SearchAvailableHallsQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<GetHallDTO>> Handle(SearchAvailableHallsQuery request, CancellationToken cancellationToken)
    {
        var startDate = ParseDate(request.StartDate, nameof(request.StartDate));
        var endDate = ParseDate(request.EndDate, nameof(request.EndDate));

        if (endDate <= startDate)
        {
            throw new ArgumentException("EndDate must be after StartDate.", nameof(request.EndDate));
        }

        if (request.Capacity <= 0)
        {
            throw new ArgumentException("Capacity must be a positive number.", nameof(request.Capacity));
        }

        // Зали, що підходять за місткістю і не мають бронювань, які перетинаються з запитаним
        // проміжком (той самий предикат перетину, що й у CreateBookingCommandHandler).
        var halls = await _context.Halls
            .Where(h => !h.Removed
                && h.Capacity >= request.Capacity
                && !_context.Bookings.Any(b =>
                    b.OriginalHallId == h.Id
                    && !b.Removed
                    && b.StartDate < endDate
                    && b.EndDate > startDate))
            .Include(h => h.Services)
            .ToListAsync(cancellationToken);

        return halls.Select(GetHallDTO.FromEntity).ToList();
    }

    private static DateTime ParseDate(string value, string paramName)
    {
        if (!DateTime.TryParseExact(value, DdMmYyyyDateTimeConverter.Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw new ArgumentException($"Could not parse '{value}' as a date. Expected format: '{DdMmYyyyDateTimeConverter.Format}'.", paramName);
        }

        return result;
    }
}
