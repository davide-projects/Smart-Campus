using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", table =>
        {
            // INV-2: la fine è dopo l'inizio.
            table.HasCheckConstraint("CK_Reservations_TimeRange", "[End] > [Start]");
            table.HasCheckConstraint("CK_Reservations_Participants_Positive", "[Participants] > 0");
        });

        builder.HasKey(reservation => reservation.Id);

        // Valori leggibili nel database ("Confirmed", "Cancelled") e sicuri se l'enum cambia.
        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(reservation => reservation.Purpose)
            .HasMaxLength(200);

        // Versione della riga gestita da SQL Server: blocca le modifiche basate su dati vecchi.
        builder.Property(reservation => reservation.RowVersion)
            .IsRowVersion();

        // Uno spazio con prenotazioni non può essere eliminato: va dismesso (IsActive = false).
        builder.HasOne(reservation => reservation.Room)
            .WithMany()
            .HasForeignKey(reservation => reservation.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Per la ricerca delle sovrapposizioni sullo stesso spazio (INV-1) e per la disponibilità.
        builder.HasIndex(reservation => new { reservation.RoomId, reservation.Status, reservation.Start });

        // Per "le mie prenotazioni" e per INV-8 (stesso organizzatore in due posti).
        builder.HasIndex(reservation => new { reservation.OrganizerId, reservation.Start });
    }
}