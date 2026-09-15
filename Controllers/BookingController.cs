using MediatR;
using Microsoft.AspNetCore.Mvc;
using SpaceCore.DTOs;
using SpaceCore.Handlers;

namespace SpaceCore.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BookingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get the list of all bookings, along with the booked services
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GetBookingDTO>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBookingsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new hall booking
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BookingConfirmationDTO>> CreateBooking(
        [FromBody] CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send the command through MediatR to our handler
            var result = await _mediator.Send(command, cancellationToken);

            // Return the result with 200 OK (201 Created could be used if needed)
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}