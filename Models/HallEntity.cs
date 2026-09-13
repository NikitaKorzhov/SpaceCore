namespace SpaceCore.Models;

public class HallEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Capacity { get; set; }
    public Boolean Removed { get; set; } = false;
    public decimal PricePerHour { get; set; }
    
    public ICollection<ServiceEntity> Services { get; set; } = new List<ServiceEntity>();
}