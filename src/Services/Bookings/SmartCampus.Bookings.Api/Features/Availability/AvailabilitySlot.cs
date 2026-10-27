namespace SmartCampus.Bookings.Api.Features.Availability;

// Una fascia di 30 minuti (assunzione A1) dentro l'orario di apertura.
// Dice solo se è libera: nessun dato su chi ha prenotato.
public sealed record AvailabilitySlot(DateTimeOffset Start, DateTimeOffset End, bool IsFree);
