using MassTransit;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data;

// Database del servizio Rooms. Nessun altro servizio legge o scrive queste tabelle.
public class RoomsDbContext(DbContextOptions<RoomsDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<OpeningHours> OpeningHours => Set<OpeningHours>();

    public DbSet<RoomSeedEventDispatch> RoomSeedEventDispatches => Set<RoomSeedEventDispatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomsDbContext).Assembly);
        modelBuilder.Entity<RoomSeedEventDispatch>(entity =>
        {
            entity.ToTable("RoomSeedEventDispatches");
            entity.HasKey(dispatch => dispatch.Id);
            entity.Property(dispatch => dispatch.Id).ValueGeneratedNever();
        });
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        SeedData.Apply(modelBuilder);
    }
}
