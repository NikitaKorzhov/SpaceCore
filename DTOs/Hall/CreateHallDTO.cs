using System.ComponentModel.DataAnnotations;

namespace SpaceCore.DTOs.Hall;

public class CreateHallDTO
{
    public string Name { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be a positive number.")]
    public int Capacity { get; set; }
    // Bounds are config-driven; see IHallValidationService.ValidatePrice, called from the handlers.
    public decimal Price { get; set; }
    public List<CreateServiceDTO> Services { get; set; } = new();

}
public record UpdateHallDTO(
    string Name,
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be a positive number.")] int Capacity,
    decimal Price,
    List<UpdateServiceDTO>? Services
);

