using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");

        builder.HasKey(record => record.Id);

        // Valori leggibili nel database ("CheckIn", "NoShow"), come lo stato della prenotazione.
        builder.Property(record => record.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Se la prenotazione viene eliminata, il suo record di presenza se ne va con lei.
        builder.HasOne(record => record.Reservation)
            .WithMany(reservation => reservation.AttendanceRecords)
            .HasForeignKey(record => record.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un solo record di presenza per prenotazione, CheckIn o NoShow: difesa nel database anche
        // contro scritture che non prendono il lock sulla prenotazione (design della change 45-check-in, decisione 4).
        builder.HasIndex(record => record.ReservationId)
            .IsUnique();
    }
}
