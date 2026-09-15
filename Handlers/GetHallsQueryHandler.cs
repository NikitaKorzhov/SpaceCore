using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
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

        return halls.Select(GetHallDTO.FromEntity).ToList();
    }
}