using SmartCampus.Bookings.Api.Entities;
using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Availability;

// Calcolo puro della disponibilità: riceve tutti i dati come parametri e non usa database né orologio.
// Per questo si verifica con unit test veloci.
public static class AvailabilityCalculator
{
    // Assunzione A1: si prenota a blocchi di 30 minuti.
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    public static DailyAvailability Calculate(
        Guid roomId,
        DateOnly date,
        BookableRoomOpeningHours? openingHours,
        IEnumerable<Reservation> reservations,
        TimeZoneInfo campusTimeZone)
    {
        // Nessuna fascia di apertura per quel giorno: lo spazio è chiuso, e lo diciamo esplicitamente.
        if (openingHours is null)
        {
            return new DailyAvailability(roomId, date, IsOpen: false, Slots: []);
        }

        // Solo le prenotazioni confermate occupano lo spazio (INV-1): le cancellate non contano.
        var confirmed = reservations
            .Where(reservation => reservation.Status == ReservationStatus.Confirmed)
            .ToList();

        var slots = new List<AvailabilitySlot>();

        // Si scorre l'orario di apertura a passi di 30 minuti. Si generano solo fasce intere:
        // se l'orario non è un multiplo di 30 minuti, l'ultimo pezzo più corto non è prenotabile.
        for (var time = openingHours.OpensAt;
             time.Add(SlotLength) <= openingHours.ClosesAt && time.Add(SlotLength) > time;
             time = time.Add(SlotLength))
        {
            var start = ToCampusTime(date, time, campusTimeZone);
            var end = ToCampusTime(date, time.Add(SlotLength), campusTimeZone);

            // Intervalli semiaperti [Start, End): una prenotazione che finisce alle 10:00
            // non occupa la fascia 10:00-10:30.
            var isFree = !confirmed.Any(reservation => reservation.Start < end && reservation.End > start);

            slots.Add(new AvailabilitySlot(start, end, isFree));
        }

        return new DailyAvailability(roomId, date, IsOpen: true, slots);
    }

    // Combina data e ora locali del campus in un istante preciso, con lo scarto UTC giusto per quel giorno
    // (+01:00 d'inverno, +02:00 con l'ora legale).
    private static DateTimeOffset ToCampusTime(DateOnly date, TimeOnly time, TimeZoneInfo campusTimeZone)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, campusTimeZone.GetUtcOffset(local));
    }
}
