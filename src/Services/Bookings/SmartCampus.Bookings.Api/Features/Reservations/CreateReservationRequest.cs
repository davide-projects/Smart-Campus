namespace SmartCampus.Bookings.Api.Features.Reservations;

// Richiesta di prenotazione singola.
// Chi prenota non è qui: si legge dal token inoltrato dal Gateway.
public sealed record CreateReservationRequest(
    Guid RoomId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Purpose);
