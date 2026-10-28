using SmartCampus.Bookings.Api.Entities.Enums;

namespace SmartCampus.Bookings.Api.Features.Reservations;

// Conferma restituita con il 201: spazio, giorno e orario della prenotazione (US-04).
public sealed record ReservationResponse(
    Guid Id,
    Guid RoomId,
    string RoomName,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Participants,
    ReservationStatus Status);
