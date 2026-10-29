namespace SmartCampus.Bookings.Api.Features.Reservations;

// Risposta di GET /api/bookings/mine (US-07): le tre sezioni della pagina "Le mie prenotazioni".
public sealed record MyReservationsResponse(
    IReadOnlyList<MyReservationItem> Upcoming,
    IReadOnlyList<MyReservationItem> Past,
    IReadOnlyList<MyReservationItem> Cancelled);

// Una prenotazione dell'utente, come organizzatore o come membro.
// BuildingName e OrganizerName sono null quando Bookings non li conosce (dati precedenti a US-07).
// CheckedInAt è l'istante del check-in (US-14), fatto da chiunque; null se la prenotazione non ce l'ha.
public sealed record MyReservationItem(
    Guid Id,
    Guid RoomId,
    string RoomName,
    string? BuildingName,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsGroup,
    string? OrganizerName,
    bool IsOrganizer,
    DateTimeOffset? CheckedInAt);
