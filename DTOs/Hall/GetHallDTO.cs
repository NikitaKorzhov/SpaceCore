using SpaceCore.DTOs;
using SpaceCore.Models;

namespace SpaceCore.DTOs.Hall;

public class GetHallDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public Boolean Removed { get; set; }
    public List<GetServiceDTO> Services { get; set; }

    public static GetHallDTO FromEntity(HallEntity hall)
    {
        return new GetHallDTO
        {
            Id = hall.Id,
            Name = hall.Name,
            Capacity = hall.Capacity,
            Price = hall.PricePerHour,
            Removed = hall.Removed,
            Services = hall.Services
                .Where(s => !s.Removed) // Filter out removed services
                .Select(s => new GetServiceDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price
                }).ToList()
        };
    }
}