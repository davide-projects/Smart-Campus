namespace SmartCampus.Bookings.Api.Features.Reservations;

// Risposta di GET /api/bookings/mine (US-07): le tre sezioni della pagina "Le mie prenotazioni".
public sealed record MyReservationsResponse(
    IReadOnlyList<MyReservationItem> Upcoming,
    IReadOnlyList<MyReservationItem> Past,
    IReadOnlyList<MyReservationItem> Cancelled);

// Una prenotazione dell'utente, come organizzatore o come membro.
// BuildingName e OrganizerName sono null quando Bookings non li conosce (dati precedenti a US-07).
// CheckedInAt è l'istante del check-in (US-14), fatto da chiunque; null se la prenotazione non ce l'ha.
// Members sono i membri del gruppo, organizzatore escluso, in ordine di nome; [] per una prenotazione singola.
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
    DateTimeOffset? CheckedInAt,
    IReadOnlyList<MyReservationMember> Members);

// Un membro di una prenotazione di gruppo. DisplayName è null quando Bookings non conosce il nome
// (membri salvati prima di US-05 o nome non ricevuto da Identity).
public sealed record MyReservationMember(Guid Id, string? DisplayName);
