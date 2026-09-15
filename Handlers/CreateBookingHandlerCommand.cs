using System.Collections.Concurrent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpaceCore.Data;
using SpaceCore.DTOs;
using SpaceCore.Models;
using SpaceCore.Services;
using SpaceCore.Services.Domain;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingConfirmationDTO>
{
    // Serializes the overlap check and booking insertion per hall within the process,
    // so that two concurrent requests for the same hall don't both slip past the AnyAsync check.
    // This is an in-memory, single-process lock only: it does NOT protect against race conditions
    // if the app is scaled out to multiple instances (a distributed lock or a DB-level unique/exclusion
    // constraint would be needed for that).
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> HallLocks = new();

    private readonly AppDbContext _context;
    private readonly ICalculatePriceService _priceService;
    private readonly IHallService _hallService;

    public CreateBookingCommandHandler(AppDbContext context, ICalculatePriceService priceService, IHallService hallService)
    {
        _context = context;
        _priceService = priceService;
        _hallService = hallService;
    }

    public async Task<BookingConfirmationDTO> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate and fetch the hall
        var hall = await _hallService.GetByID(request.HallId, cancellationToken);

        if (hall == null)
        {
            throw new KeyNotFoundException($"Hall with ID {request.HallId} was not found.");
        }

        // Booking duration must be positive
        if (request.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be a positive time span.", nameof(request.Duration));
        }

        // Backdated bookings don't make sense
        if (request.StartDate < DateTime.Now)
        {
            throw new ArgumentException("StartDate cannot be in the past.", nameof(request.StartDate));
        }

        // Calculate the booking end time
        var endDate = request.StartDate.Add(request.Duration);

        var hallLock = HallLocks.GetOrAdd(request.HallId, _ => new SemaphoreSlim(1, 1));
        await hallLock.WaitAsync(cancellationToken);
        try
        {
            // Check that the hall is free for this time range.
            // Classic interval-overlap test: two ranges [A.Start, A.End) and [B.Start, B.End) overlap
            // whenever A.Start < B.End AND A.End > B.Start (equivalent to NOT(A ends before B starts
            // OR A starts after B ends)). The same predicate is reused in SearchAvailableHallsQueryHandler.
            var hasOverlap = await _context.Bookings.AnyAsync(
                b => b.OriginalHallId == request.HallId
                     && !b.Removed
                     && b.StartDate < endDate
                     && b.EndDate > request.StartDate,
                cancellationToken);

            if (hasOverlap)
            {
                throw new InvalidOperationException("The hall is already booked for the requested time range.");
            }

            return await CreateBookingAsync(request, hall, endDate, cancellationToken);
        }
        finally
        {
            hallLock.Release();
        }
    }

    private async Task<BookingConfirmationDTO> CreateBookingAsync(
        CreateBookingCommand request, SpaceCore.DTOs.Hall.GetHallDTO hall, DateTime endDate, CancellationToken cancellationToken)
    {
        // 2. Calculate the hall rental price through the pricing service, accounting for multipliers
        decimal hallPrice = _priceService.Calculate(request.StartDate, endDate, hall.Price);

        // 3. Process additional services and create their historical snapshots (ServiceFreeze).
        // We copy the Name/Price into a separate ServiceFreezeEntity rather than just referencing
        // the ServiceEntity by ID, because the hall's services (name/price) can change or be removed
        // later — the booking must keep showing what the customer actually agreed to pay at booking time.
        var serviceFreezes = new List<ServiceFreezeEntity>();
        decimal servicesTotalCost = 0m;

        if (request.ServiceIds != null && request.ServiceIds.Any())
        {
            // Services must belong specifically to this hall
            var hallServiceIds = hall.Services.Select(s => s.Id).ToHashSet();
            var unknownServiceIds = request.ServiceIds.Where(id => !hallServiceIds.Contains(id)).ToList();

            if (unknownServiceIds.Any())
            {
                throw new ArgumentException(
                    $"The following services are not available for hall {request.HallId}: {string.Join(", ", unknownServiceIds)}",
                    nameof(request.ServiceIds));
            }

            var services = hall.Services.Where(s => request.ServiceIds.Contains(s.Id));

            foreach (var service in services)
            {
                serviceFreezes.Add(new ServiceFreezeEntity
                {
                    Id = Guid.NewGuid(),
                    OriginalId = service.Id,
                    Name = service.Name,
                    Price = service.Price,
                    FreezeDate = DateTime.UtcNow
                });

                servicesTotalCost += service.Price;
            }
        }

        // Total amount (hall rental accounting for multipliers + services)
        decimal finalTotalPrice = hallPrice + servicesTotalCost;

        // 4. Create the booking entity
        var booking = new BookingEntity
        {
            Id = Guid.NewGuid(),
            OriginalHallId = hall.Id,
            StartDate = request.StartDate,
            EndDate = endDate,
            PricePerHour = hall.Price, // Store the rate at the time of booking
            TotalPrice = finalTotalPrice,
            Services = serviceFreezes
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Build the outgoing confirmation for the client
        return new BookingConfirmationDTO
        {
            BookingId = booking.Id,
            HallName = hall.Name,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            TotalPrice = booking.TotalPrice,
            Services = serviceFreezes.Select(sf => new BookedServiceDTO
            {
                Name = sf.Name,
                Price = sf.Price
            }).ToList()
        };
    }
}