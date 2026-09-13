namespace SpaceCore.DTOs.Hall;

public class GetHallDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public Boolean Removed { get; set; }
    public List<GetServiceDTO> Services { get; set; }
}