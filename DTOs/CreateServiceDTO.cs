namespace SpaceCore.DTOs;

public class CreateServiceDTO
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}
public record UpdateServiceDTO(
    Guid? Id, 
    string Name, 
    decimal Price
);
