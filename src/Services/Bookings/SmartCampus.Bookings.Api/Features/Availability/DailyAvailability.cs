namespace SmartCampus.Bookings.Api.Features.Availability;

// Disponibilità di uno spazio in un giorno.
// IsOpen = false indica esplicitamente che lo spazio è chiuso quel giorno (e Slots è vuota).
public sealed record DailyAvailability(Guid RoomId, DateOnly Date, bool IsOpen, IReadOnlyList<AvailabilitySlot> Slots);
