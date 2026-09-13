using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;

namespace SpaceCore.Handlers;

public record DeleteHallCommand(Guid Id) : IRequest<bool>;

public class DeleteHallCommandHandler : IRequestHandler<DeleteHallCommand, bool>
{
    private readonly AppDbContext _context;

    public DeleteHallCommandHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteHallCommand request, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls
            .Include(h => h.Services)
            .FirstOrDefaultAsync(h => h.Id == request.Id, cancellationToken);

        if (hall == null) return false;

        hall.Removed = true;

        foreach (var service in hall.Services)
        {
            service.Removed = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}