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
    // Отримати всі зали
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
    // Пошук вільних залів за датою, часовим проміжком та потрібною місткістю
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
    // Додати новий зал
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHallDTO dto)
    {
        var createdHall = await _mediator.Send(new CreateHallCommand(dto));
        return Ok(new { Message = "Створено!", Hall = createdHall });
    }
    // Update Hall by id
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateHall(Guid id, [FromBody] UpdateHallDTO dto, CancellationToken cancellationToken)
    {
        var command = new UpdateHallCommand(id, dto);
        var success = await _mediator.Send(command, cancellationToken);

        if (!success)
        {
            return NotFound(new { message = "Зал не знайдено або він вже видалений." });
        }

        return Ok(new { message = "Зал успішно оновлено." });
    }
    // Видалити зал за ID
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _mediator.Send(new DeleteHallCommand(id));
        if (!deleted) return NotFound("Зал не знайдено");
        return Ok("Зал та пов'язані послуги успішно видалено!");
    }
}
