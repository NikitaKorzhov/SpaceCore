namespace SpaceCore.DTOs.Hall;

public class CreateHallDTO
{
    public string Name { get; set; }
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public List<CreateServiceDTO> Services { get; set; } = new();
    
}
public record UpdateHallDTO(
    string Name,
    int Capacity,
    decimal Price,
    List<UpdateServiceDTO>? Services
);

