using System.Text.Json.Serialization;
using MediatR;
using SpaceCore.Common;

namespace SpaceCore.DTOs;

// Incoming command from the controller
public record CreateBookingCommand(
    Guid HallId,
    [property: JsonConverter(typeof(DdMmYyyyDateTimeConverter))] DateTime StartDate,
    TimeSpan Duration,
    List<Guid> ServiceIds
) : IRequest<BookingConfirmationDTO>;

// Outgoing DTO with the confirmation
public class BookingConfirmationDTO
{
    public Guid BookingId { get; set; }
    public string HallName { get; set; } = string.Empty;
    [JsonConverter(typeof(DdMmYyyyDateTimeConverter))]
    public DateTime StartDate { get; set; }
    [JsonConverter(typeof(DdMmYyyyDateTimeConverter))]
    public DateTime EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public List<BookedServiceDTO> Services { get; set; } = new();
}

public class BookedServiceDTO
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Item in the list of all bookings, along with the booked services
public class GetBookingDTO
{
    public Guid Id { get; set; }
    public Guid HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    [JsonConverter(typeof(DdMmYyyyDateTimeConverter))]
    public DateTime StartDate { get; set; }
    [JsonConverter(typeof(DdMmYyyyDateTimeConverter))]
    public DateTime EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public List<BookedServiceDTO> Services { get; set; } = new();
}
