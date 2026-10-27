using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Configurations;

internal sealed class BookableRoomOpeningHoursConfiguration : IEntityTypeConfiguration<BookableRoomOpeningHours>
{
    public void Configure(EntityTypeBuilder<BookableRoomOpeningHours> builder)
    {
        builder.ToTable("BookableRoomOpeningHours", table =>
            table.HasCheckConstraint("CK_BookableRoomOpeningHours_TimeRange", "[ClosesAt] > [OpensAt]"));

        builder.HasKey(hours => new { hours.RoomId, hours.DayOfWeek });

        builder.Property(hours => hours.DayOfWeek)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.HasOne(hours => hours.Room)
            .WithMany(room => room.OpeningHours)
            .HasForeignKey(hours => hours.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
