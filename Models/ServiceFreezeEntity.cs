namespace SpaceCore.Models;

public class ServiceFreezeEntity
{
    public Guid Id { get; set; }
    public Guid OriginalId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public Boolean Removed { get; set; }=false;
    public DateTime FreezeDate { get; set; }
}