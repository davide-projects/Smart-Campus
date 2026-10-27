using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartCampus.Bookings.Api.Entities;

namespace SmartCampus.Bookings.Api.Data.Configurations;

internal sealed class ReservationMemberConfiguration : IEntityTypeConfiguration<ReservationMember>
{
    public void Configure(EntityTypeBuilder<ReservationMember> builder)
    {
        builder.ToTable("ReservationMembers");

        // Chiave composta: lo stesso studente non può essere due volte nella stessa prenotazione.
        builder.HasKey(member => new { member.ReservationId, member.UserId });

        // Se la prenotazione viene eliminata, i suoi membri se ne vanno con lei.
        builder.HasOne(member => member.Reservation)
            .WithMany(reservation => reservation.Members)
            .HasForeignKey(member => member.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Per trovare in fretta le prenotazioni di uno studente come membro (INV-8, "le mie prenotazioni").
        builder.HasIndex(member => member.UserId);
    }
}
