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
    }
}