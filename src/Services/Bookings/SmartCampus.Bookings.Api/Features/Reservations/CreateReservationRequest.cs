namespace SmartCampus.Bookings.Api.Features.Reservations;

// Richiesta di prenotazione, singola o di gruppo.
// Chi prenota non è qui: si legge dal token inoltrato dal Gateway.
// MemberIds sono gli id degli altri studenti del gruppo: null o vuoto vuol dire prenotazione singola.
public sealed record CreateReservationRequest(
    Guid RoomId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Purpose,
    IReadOnlyList<Guid>? MemberIds = null);
