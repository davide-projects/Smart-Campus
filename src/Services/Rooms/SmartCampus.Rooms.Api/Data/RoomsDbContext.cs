using Microsoft.EntityFrameworkCore;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

// Database del servizio Rooms. Nessun altro servizio legge o scrive queste tabelle.
public class RoomsDbContext(DbContextOptions<RoomsDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<OpeningHours> OpeningHours => Set<OpeningHours>();

    public DbSet<RoomEquipment> RoomEquipment => Set<RoomEquipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomsDbContext).Assembly);

        SeedData.Apply(modelBuilder);
    }
}
