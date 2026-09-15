namespace SpaceCore.Models;

public class ServiceEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public Boolean Removed { get; set; }=false;
    // Declared as a many-to-many navigation (EF Core convention, since there's no matching FK/DbContext
    // config for a one-to-many). In practice every service is created together with a single hall and
    // never shared, so this collection normally holds exactly one entry.
    public ICollection<HallEntity> Hall { get; set; } = new List<HallEntity>();

}