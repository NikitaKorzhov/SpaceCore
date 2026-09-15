using Microsoft.EntityFrameworkCore;
using SpaceCore.Models;

namespace SpaceCore.Data;

public class AppDbContext: DbContext
{
    public DbSet<HallEntity> Halls { get; set; }
    public DbSet<ServiceEntity> Services { get; set; }
    public DbSet<BookingEntity> Bookings { get; set; }
    public DbSet<ServiceFreezeEntity>  ServiceFreezes { get; set; }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}