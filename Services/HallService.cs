using MediatR;
using SpaceCore.DTOs.Hall;
using SpaceCore.Handlers;

namespace SpaceCore.Services;

public interface IHallService
{
  Task<GetHallDTO?> GetByID(Guid id, CancellationToken cancellationToken);
}
public class HallService:IHallService
{
    private readonly IMediator _mediator;
    public HallService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public Task<GetHallDTO?> GetByID(Guid id, CancellationToken cancellationToken)
    {
        return _mediator.Send(new GetHallByIdQuery(id), cancellationToken);
    }
}
