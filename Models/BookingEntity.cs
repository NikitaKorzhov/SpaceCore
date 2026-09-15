namespace SpaceCore.Models;

public class BookingEntity
{
    public Guid Id { get; set; }
    public Guid OriginalHallId { get; set; }
    public Boolean Removed { get; set; } = false;
    public decimal PricePerHour { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public ICollection<ServiceFreezeEntity> Services { get; set; } = new List<ServiceFreezeEntity>();
}