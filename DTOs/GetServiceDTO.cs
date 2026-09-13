namespace SpaceCore.DTOs;

public class GetServiceDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool Removed { get; set; }
}