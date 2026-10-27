using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms", table =>
            table.HasCheckConstraint("CK_Rooms_Capacity_Positive", "[Capacity] > 0"));

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Valori leggibili nel database ("Classroom", "StudyRoom"...), non numeri.
        builder.Property(room => room.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Il nome di uno spazio è univoco all'interno del suo edificio.
        builder.HasIndex(room => new { room.BuildingId, room.Name })
            .IsUnique();

        // Indice per la ricerca pubblica: spazi attivi filtrati per tipo e capienza.
        builder.HasIndex(room => new { room.IsActive, room.Type, room.Capacity });

        builder.HasOne(room => room.Building)
            .WithMany(building => building.Rooms)
            .HasForeignKey(room => room.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
