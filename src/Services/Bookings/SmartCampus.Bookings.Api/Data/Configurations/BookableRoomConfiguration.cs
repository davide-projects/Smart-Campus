using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Configurations;

internal sealed class BookableRoomConfiguration : IEntityTypeConfiguration<BookableRoom>
{
    public void Configure(EntityTypeBuilder<BookableRoom> builder)
    {
        builder.ToTable("BookableRooms");

        builder.HasKey(room => room.RoomId);

        builder.Property(room => room.RoomId)
            .ValueGeneratedNever();

        builder.Property(room => room.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Stessa lunghezza di Building.Name in Rooms.
        builder.Property(room => room.BuildingName)
            .HasMaxLength(100);

        // Nome di RoomType in Rooms: "Classroom", "Laboratory" o "StudyRoom".
        builder.Property(room => room.Type)
            .HasMaxLength(30);
    }
}
