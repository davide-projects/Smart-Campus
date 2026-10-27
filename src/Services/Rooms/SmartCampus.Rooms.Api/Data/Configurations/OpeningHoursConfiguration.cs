using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Rooms.Models;

namespace SmartCampus.Rooms.Data.Configurations;

internal sealed class OpeningHoursConfiguration : IEntityTypeConfiguration<OpeningHours>
{
    public void Configure(EntityTypeBuilder<OpeningHours> builder)
    {
        builder.ToTable("OpeningHours", table =>
            table.HasCheckConstraint("CK_OpeningHours_TimeRange", "[ClosesAt] > [OpensAt]"));

        builder.HasKey(openingHours => openingHours.Id);

        // Giorno salvato come testo ("Monday"...), più leggibile di un intero.
        builder.Property(openingHours => openingHours.DayOfWeek)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        // The initial implementation allows one opening interval per room and day.
        builder.HasIndex(openingHours => new { openingHours.RoomId, openingHours.DayOfWeek })
            .IsUnique();

        builder.HasOne(openingHours => openingHours.Room)
            .WithMany(room => room.OpeningHours)
            .HasForeignKey(openingHours => openingHours.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
