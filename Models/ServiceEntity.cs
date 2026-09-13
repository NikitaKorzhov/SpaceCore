namespace SpaceCore.Models;

public class ServiceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public Boolean Removed { get; set; }=false;
    public ICollection<HallEntity> Hall { get; set; } = new List<HallEntity>();

}