using MediatR;
using Microsoft.AspNetCore.Mvc;
using SpaceCore.DTOs.Hall;
using SpaceCore.Handlers;

namespace SpaceCore.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HallsController : ControllerBase
{
    private readonly IMediator _mediator;

    public HallsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    // Get all halls
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GetHallDTO>>> GetAll()
    {
        var r =await _mediator.Send(new GetHallsQuery());
        return Ok(r);
    }
    // Get Hall by id
    [HttpGet("{id}")]
    public async Task<ActionResult<GetHallDTO>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetHallByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }
    // Search for available halls by date, time range, and required capacity.
    // SearchAvailableHallsQuery is bound directly from the query string (?StartDate=...&EndDate=...&Capacity=...)
    // because ASP.NET Core can bind [FromQuery] to a positional record's constructor parameters by name —
    // no separate query-DTO is needed, the MediatR request doubles as the model-binding target.
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<GetHallDTO>>> SearchAvailable(
        [FromQuery] SearchAvailableHallsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    // Add a new hall
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHallDTO dto)
    {
        try
        {
            var createdHall = await _mediator.Send(new CreateHallCommand(dto));
            return Ok(new { Message = "Created!", Hall = createdHall });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    // Update Hall by id
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateHall(Guid id, [FromBody] UpdateHallDTO dto, CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateHallCommand(id, dto);
            var success = await _mediator.Send(command, cancellationToken);

            if (!success)
            {
                return NotFound(new { message = "Hall not found or already removed." });
            }

            return Ok(new { message = "Hall successfully updated." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    // Delete a hall by ID
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _mediator.Send(new DeleteHallCommand(id));
        if (!deleted) return NotFound("Hall not found");
        return Ok("Hall and its related services successfully deleted!");
    }
}
